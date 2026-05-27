using AIAgentChat.Application.Models;

namespace AIAgentChat.Application.Extensions;

internal static class KnowledgeChunkExtensions
{
    /// <summary>
    /// Creates a copy of the chunk with a calculated score.
    /// 
    /// KnowledgeChunk is a class, not a record, so we use this helper
    /// to keep scoring code readable.
    /// </summary>
    public static KnowledgeChunk WithScore(this KnowledgeChunk chunk, int score)
    {
        return new KnowledgeChunk
        {
            Index = chunk.Index,
            Source = chunk.Source,
            Content = chunk.Content,
            Score = score
        };
    }
}