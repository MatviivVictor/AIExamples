using AIAgentChat.Application.Models;

namespace AIAgentChat.Application.Services;

public class RagAnswerResult
{
    public string Answer { get; set; } = string.Empty;
    public IReadOnlyList<KnowledgeChunk> RetrievedChunks { get; set; } = [];
    public bool Answered { get; set; }
    public string CannotAnswerReason { get; set; } = string.Empty;
}
