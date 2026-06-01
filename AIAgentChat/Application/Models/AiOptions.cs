namespace AIAgentChat.Application.Models;

internal sealed class AiOptions
{
    public string Provider { get; set; } = "Ollama";

    public string Endpoint { get; set; } = "http://localhost:11434";

    public string Model { get; set; } = "llama3";

    public string? EmbeddingModel { get; set; } = "all-minilm";

    public string? ApiKeyEnvironmentVariable { get; set; }

    public string? SystemPrompt { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Provider))
        {
            throw new InvalidOperationException("AI provider is not configured.");
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            throw new InvalidOperationException("AI model is not configured.");
        }

        if (IsOllama() && string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new InvalidOperationException("Ollama endpoint is not configured.");
        }

        if ((IsOpenAI() || IsGemini()) && string.IsNullOrWhiteSpace(ApiKeyEnvironmentVariable))
        {
            throw new InvalidOperationException(
                $"API key environment variable is required for provider '{Provider}'.");
        }

        if (IsGemini() && string.IsNullOrWhiteSpace(Endpoint))
        {
            throw new InvalidOperationException("Gemini endpoint is not configured.");
        }

        if (!IsOllama() && !IsOpenAI() && !IsGemini())
        {
            throw new NotSupportedException($"Provider '{Provider}' is not supported.");
        }
    }

    public bool IsOllama()
    {
        return string.Equals(Provider, "Ollama", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsOpenAI()
    {
        return string.Equals(Provider, "OpenAI", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsGemini()
    {
        return string.Equals(Provider, "Gemini", StringComparison.OrdinalIgnoreCase);
    }
}