namespace OllamaChat.Application.Models;

internal sealed class AiOptions
{
    public string Provider { get; init; } = "Ollama";

    public string Endpoint { get; init; } = "http://localhost:11434";

    public string Model { get; init; } = "llama3";

    public string? SystemPrompt { get; init; }
}