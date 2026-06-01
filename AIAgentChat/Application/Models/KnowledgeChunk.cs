namespace AIAgentChat.Application.Models;

/// <summary>
/// Represents one searchable piece of documentation.
/// 
/// In RAG systems, large documents are usually split into smaller fragments.
/// These fragments are called "chunks".
/// 
/// The application searches among chunks, selects the most relevant ones,
/// and sends only those chunks to the AI model as context.
/// </summary>
internal sealed class KnowledgeChunk
{
    /// <summary>
    /// Chunk identifier inside the source document.
    /// Useful for debugging and showing where the answer came from.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Original file path of the document.
    /// Example: Manuals/user-guid.md.
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Actual text content of the chunk.
    /// This text will be passed to the LLM as RAG context.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Simple relevance score calculated by keyword matching or vector similarity.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Vector representation of the chunk content.
    /// </summary>
    public ReadOnlyMemory<float> Embedding { get; init; }
}