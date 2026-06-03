using AIAgentChat.Application.Models;

namespace AIAgentChat.Application.Services;

public sealed class RagGuardrailService
{
    private readonly string[] _suspiciousInstructions =
    [
        "ignore previous instructions",
        "system prompt",
        "developer message",
        "do not follow user",
        "you are now",
        "reveal secrets",
        "send api key"
    ];

    /// <summary>
    /// Фільтрує знайдені фрагменти знань на предмет підозрілих інструкцій.
    /// RAG context не можна вважати довіреним, бо він може містити prompt injection всередині документів.
    /// </summary>
    public List<KnowledgeChunk> FilterChunks(IEnumerable<KnowledgeChunk> chunks)
    {
        var safeChunks = new List<KnowledgeChunk>();

        foreach (var chunk in chunks)
        {
            if (IsSuspicious(chunk.Content))
            {
                // Для навчального проєкту ми просто пропускаємо підозрілий чанк.
                // В реальних системах можна було б додати логування або посилений промпт.
                continue;
            }

            safeChunks.Add(chunk);
        }

        return safeChunks;
    }

    private bool IsSuspicious(string content)
    {
        var normalized = content.ToLowerInvariant();
        
        foreach (var pattern in _suspiciousInstructions)
        {
            if (normalized.Contains(pattern))
            {
                return true;
            }
        }

        return false;
    }
}
