namespace AIAgentChat.Application.Models;

public record CacheOptions
{
    public bool Enabled { get; init; } = true;
    public int RagRetrievalTtlMinutes { get; init; } = 30;
    public int RagAnswerTtlMinutes { get; init; } = 15;
    public int ClassificationTtlMinutes { get; init; } = 30;
    public int EmbeddingTtlHours { get; init; } = 24;
    public int MaxPreviewLength { get; init; } = 80;
}
