using System.Text.RegularExpressions;
using AIAgentChat.Application.Extensions;
using AIAgentChat.Application.Models;

namespace AIAgentChat.Application.Services;

/// <summary>
/// Provides a very simple local knowledge base for RAG.
/// 
/// This is an educational implementation of retrieval.
/// It does not use embeddings yet.
/// Instead, it uses:
/// - Markdown document loading;
/// - chunking;
/// - keyword-based relevance scoring.
/// 
/// Later this service can be replaced or extended with:
/// - embeddings;
/// - vector database;
/// - hybrid search;
/// - reranking.
/// </summary>
internal sealed class KnowledgeBaseService
{
    private const int MaxChunkLength = 1_500;

    private readonly string _manualPath;

    private IReadOnlyList<KnowledgeChunk>? _cachedChunks;

    public KnowledgeBaseService(string manualPath)
    {
        _manualPath = manualPath;
    }

    /// <summary>
    /// Searches the knowledge base and returns the most relevant chunks.
    /// 
    /// The current search is intentionally simple:
    /// - extract words from the user question;
    /// - compare them with each chunk;
    /// - score chunks by the number of keyword matches;
    /// - return the best chunks.
    /// </summary>
    public IReadOnlyList<KnowledgeChunk> Search(string question, int maxResults = 3)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return [];
        }

        var chunks = GetOrLoadChunks();

        var queryTerms = ExtractSearchTerms(question);

        if (queryTerms.Count == 0)
        {
            return [];
        }

        return chunks
            .Select(chunk => chunk.WithScore( CalculateScore(chunk.Content, queryTerms)))
            .Where(chunk => chunk.Score > 0)
            .OrderByDescending(chunk => chunk.Score)
            .ThenBy(chunk => chunk.Index)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// Loads chunks once and keeps them in memory.
    /// 
    /// This is enough for a small console demo.
    /// In production, you may want:
    /// - indexing at application startup;
    /// - background refresh;
    /// - persistent vector store;
    /// - file change tracking.
    /// </summary>
    private IReadOnlyList<KnowledgeChunk> GetOrLoadChunks()
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

        _cachedChunks = ChunkMarkdown(markdown, _manualPath);

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
    /// Calculates simple keyword-based relevance score.
    /// 
    /// This is not semantic search.
    /// For example, "quota" and "limit" are different words here,
    /// even though they may be semantically related.
    /// 
    /// This limitation is exactly why real RAG systems use embeddings.
    /// </summary>
    private static int CalculateScore(string content, HashSet<string> queryTerms)
    {
        var contentLower = content.ToLowerInvariant();

        return queryTerms.Count(term => contentLower.Contains(term));
    }
}