using System.Text.RegularExpressions;
using System.Numerics.Tensors;
using Microsoft.Extensions.AI;
using AIAgentChat.Application.Extensions;
using AIAgentChat.Application.Models;

namespace AIAgentChat.Application.Services;

/// <summary>
/// Provides an improved local knowledge base for RAG with hybrid search and semantic ranking.
/// </summary>
internal sealed class KnowledgeBaseService
{
    private const int MaxChunkLength = 1_500;

    private readonly string _manualPath;
    private readonly IEmbeddingGenerator<string, Embedding<float>>? _embeddingGenerator;
    private readonly IChatClient? _chatClient;

    private IReadOnlyList<KnowledgeChunk>? _cachedChunks;

    public KnowledgeBaseService(
        string manualPath,
        IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator = null,
        IChatClient? chatClient = null)
    {
        _manualPath = manualPath;
        _embeddingGenerator = embeddingGenerator;
        _chatClient = chatClient;
    }

    /// <summary>
    /// Searches the knowledge base and returns the most relevant chunks using hybrid search and semantic ranking.
    /// </summary>
    public async Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string question, int maxResults = 3)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return [];
        }

        var chunks = await GetOrLoadChunksAsync();

        // 1. Keyword search (BM25-like / Simple frequency)
        var queryTerms = ExtractSearchTerms(question);
        var keywordResults = chunks
            .Select(chunk => chunk.WithScore(CalculateScore(chunk.Content, queryTerms)))
            .Where(chunk => chunk.Score > 0)
            .OrderByDescending(chunk => chunk.Score)
            .ToList();

        // 2. Vector search
        List<KnowledgeChunk> vectorResults = [];
        if (_embeddingGenerator != null)
        {
            var questionEmbedding = await _embeddingGenerator.GenerateAsync([question]);
            vectorResults = chunks
                .Select(chunk => chunk.WithScore(TensorPrimitives.CosineSimilarity(chunk.Embedding.Span, questionEmbedding[0].Vector.Span)))
                .OrderByDescending(chunk => chunk.Score)
                .ToList();
        }

        // 3. Hybrid scoring (Reciprocal Rank Fusion)
        var hybridResults = PerformRRF(keywordResults, vectorResults, topK: 10);

        // 4. Semantic Ranking (Llama)
        if (_chatClient != null && hybridResults.Count > 0)
        {
            hybridResults = await SemanticReRankingAsync(question, hybridResults, maxResults);
        }

        return hybridResults.Take(maxResults).ToList();
    }

    private static List<KnowledgeChunk> PerformRRF(
        List<KnowledgeChunk> keywordResults,
        List<KnowledgeChunk> vectorResults,
        int topK)
    {
        var scores = new Dictionary<int, double>();
        const double k = 60.0;

        for (int i = 0; i < keywordResults.Count; i++)
        {
            var chunk = keywordResults[i];
            scores[chunk.Index] = 1.0 / (k + i + 1);
        }

        for (int i = 0; i < vectorResults.Count; i++)
        {
            var chunk = vectorResults[i];
            var score = 1.0 / (k + i + 1);
            if (scores.TryGetValue(chunk.Index, out var existing))
            {
                scores[chunk.Index] = existing + score;
            }
            else
            {
                scores[chunk.Index] = score;
            }
        }

        var allChunks = keywordResults.Concat(vectorResults)
            .GroupBy(c => c.Index)
            .ToDictionary(g => g.Key, g => g.First());

        return scores
            .OrderByDescending(kvp => kvp.Value)
            .Take(topK)
            .Select(kvp => allChunks[kvp.Key].WithScore(kvp.Value))
            .ToList();
    }

    private async Task<List<KnowledgeChunk>> SemanticReRankingAsync(
        string question,
        List<KnowledgeChunk> candidates,
        int maxResults)
    {
        var candidatesText = string.Join("\n\n", candidates.Select((c, i) => $"ID: {i}\nContent: {c.Content}"));

        var prompt = $"""
            You are a semantic reranking engine.
            Given a user question and a list of documentation chunks, identify the most relevant chunks that can help answer the question.
            
            Return ONLY a comma-separated list of IDs in order of relevance, from most relevant to least relevant.
            Example: 2,0,3
            
            Question: {question}
            
            Chunks:
            {candidatesText}
            """;

        var response = await _chatClient!.GetResponseAsync(prompt);
        var responseText = response.ToString() ?? "";
        
        var rankedIndices = responseText.Split(',')
            .Select(s => int.TryParse(s.Trim(), out var id) ? id : -1)
            .Where(id => id >= 0 && id < candidates.Count)
            .Distinct()
            .ToList();

        var rankedResults = rankedIndices
            .Select(id => candidates[id])
            .ToList();

        // Add remaining candidates that weren't mentioned by LLM
        var remaining = candidates.Where(c => !rankedResults.Contains(c));
        rankedResults.AddRange(remaining);

        return rankedResults;
    }

    private async Task<IReadOnlyList<KnowledgeChunk>> GetOrLoadChunksAsync()
    {
        if (_cachedChunks is not null)
        {
            return _cachedChunks;
        }

        if (!File.Exists(_manualPath))
        {
            throw new FileNotFoundException(
                $"Knowledge base file was not found: {_manualPath}",
                _manualPath);
        }

        var markdown = File.ReadAllText(_manualPath);

        var chunks = ChunkMarkdown(markdown, _manualPath);

        if (_embeddingGenerator != null)
        {
            var contents = chunks.Select(c => c.Content).ToList();
            var embeddings = await _embeddingGenerator.GenerateAsync(contents);

            chunks = chunks.Select((chunk, i) => new KnowledgeChunk
            {
                Index = chunk.Index,
                Source = chunk.Source,
                Content = chunk.Content,
                Embedding = embeddings[i].Vector
            }).ToList();
        }

        _cachedChunks = chunks;

        return _cachedChunks;
    }

    /// <summary>
    /// Splits Markdown into smaller pieces.
    /// 
    /// This implementation tries to split by headings first,
    /// because headings usually define meaningful documentation sections.
    /// 
    /// If a section is too large, it is further split into smaller pieces.
    /// </summary>
    private static IReadOnlyList<KnowledgeChunk> ChunkMarkdown(string markdown, string source)
    {
        var normalizedMarkdown = markdown.Replace("\r\n", "\n");

        var sections = Regex.Split(
                normalizedMarkdown,
                pattern: @"(?=^#{1,3}\s+)",
                options: RegexOptions.Multiline)
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .Select(section => section.Trim())
            .ToList();

        var chunks = new List<KnowledgeChunk>();
        var index = 1;

        foreach (var section in sections)
        {
            if (section.Length <= MaxChunkLength)
            {
                chunks.Add(new KnowledgeChunk
                {
                    Index = index,
                    Source = source,
                    Content = section
                });

                index++;
                continue;
            }

            foreach (var part in SplitLongText(section, MaxChunkLength))
            {
                chunks.Add(new KnowledgeChunk
                {
                    Index = index,
                    Source = source,
                    Content = part
                });

                index++;
            }
        }

        return chunks;
    }

    /// <summary>
    /// Splits long text into smaller pieces by paragraphs.
    /// 
    /// This avoids cutting text in the middle of every sentence when possible.
    /// </summary>
    private static IEnumerable<string> SplitLongText(string text, int maxLength)
    {
        var paragraphs = text
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var current = "";

        foreach (var paragraph in paragraphs)
        {
            if (current.Length + paragraph.Length + 2 <= maxLength)
            {
                current = string.IsNullOrWhiteSpace(current)
                    ? paragraph
                    : $"{current}\n\n{paragraph}";

                continue;
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                yield return current;
            }

            current = paragraph;
        }

        if (!string.IsNullOrWhiteSpace(current))
        {
            yield return current;
        }
    }

    /// <summary>
    /// Extracts searchable words from user question.
    /// 
    /// Very short words are ignored because they usually do not help search quality.
    /// </summary>
    private static HashSet<string> ExtractSearchTerms(string question)
    {
        return Regex.Matches(question.ToLowerInvariant(), @"[\p{L}\p{N}]+")
            .Select(match => match.Value)
            .Where(word => word.Length >= 3)
            .ToHashSet();
    }

    /// <summary>
    /// Calculates simple keyword-based relevance score using term frequency.
    /// </summary>
    private static double CalculateScore(string content, HashSet<string> queryTerms)
    {
        var contentLower = content.ToLowerInvariant();
        var words = Regex.Matches(contentLower, @"[\p{L}\p{N}]+")
            .Select(m => m.Value)
            .ToList();

        if (words.Count == 0) return 0;

        double score = 0;
        foreach (var term in queryTerms)
        {
            int count = words.Count(w => w == term);
            if (count > 0)
            {
                // Simple TF-like score
                score += (double)count / words.Count;
            }
        }

        return score;
    }
}