using Microsoft.Extensions.AI;
using AIAgentChat.Application.Models;
using OllamaSharp;
using OpenAI;
using System.ClientModel;

internal static class AiChatClientFactory
{
    public static IChatClient Create(AiOptions options)
    {
        if (options.IsOllama())
        {
            return CreateOllamaClient(options);
        }

        if (options.IsOpenAI())
        {
            return CreateOpenAIClient(options);
        }

        if (options.IsGemini())
        {
            return CreateGeminiClient(options);
        }

        throw new NotSupportedException($"Provider '{options.Provider}' is not supported.");
    }

    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(AiOptions options)
    {
        if (options.IsOllama())
        {
            var httpClient = CreateOllamaHttpClient(options);

            return new OllamaApiClient(
                httpClient,
                options.EmbeddingModel ?? "nomic-embed-text");
        }

        if (options.IsOpenAI())
        {
            return CreateOpenAIEmbeddingGenerator(options);
        }

        if (options.IsGemini())
        {
            return CreateGeminiEmbeddingGenerator(options);
        }

        throw new NotSupportedException($"Provider '{options.Provider}' is not supported for embeddings.");
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateGeminiEmbeddingGenerator(AiOptions options)
    {
        var apiKey = GetRequiredApiKey(options);
        var embeddingModel = GetRequiredEmbeddingModel(options);

        var openAIClientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(options.Endpoint)
        };

        return new OpenAIClient(new ApiKeyCredential(apiKey), openAIClientOptions)
            .GetEmbeddingClient(embeddingModel)
            .AsIEmbeddingGenerator();
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateOpenAIEmbeddingGenerator(AiOptions options)
    {
        var apiKey = GetRequiredApiKey(options);
        var embeddingModel = GetRequiredEmbeddingModel(options);

        return new OpenAIClient(new ApiKeyCredential(apiKey))
            .GetEmbeddingClient(embeddingModel)
            .AsIEmbeddingGenerator();
    }
    
    private static string GetRequiredEmbeddingModel(AiOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.EmbeddingModel))
        {
            throw new InvalidOperationException(
                $"Embedding model is not configured for provider '{options.Provider}'.");
        }

        return options.EmbeddingModel;
    }
    
    private static IChatClient CreateOllamaClient(AiOptions options)
    {
        var httpClient = CreateOllamaHttpClient(options);

        return new OllamaApiClient(httpClient, options.Model);
    }

    private static HttpClient CreateOllamaHttpClient(AiOptions options)
    {
        return new HttpClient
        {
            BaseAddress = new Uri(options.Endpoint),
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };
    }

    private static IChatClient CreateOpenAIClient(AiOptions options)
    {
        var apiKey = GetRequiredApiKey(options);

        return new OpenAIClient(new ApiKeyCredential(apiKey))
            .GetChatClient(options.Model)
            .AsIChatClient();
    }

    private static IChatClient CreateGeminiClient(AiOptions options)
    {
        var apiKey = GetRequiredApiKey(options);

        var openAIClientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(options.Endpoint)
        };

        return new OpenAIClient(new ApiKeyCredential(apiKey), openAIClientOptions)
            .GetChatClient(options.Model)
            .AsIChatClient();
    }

    private static string GetRequiredApiKey(AiOptions options)
    {
        var environmentVariableName = options.ApiKeyEnvironmentVariable;

        if (string.IsNullOrWhiteSpace(environmentVariableName))
        {
            throw new InvalidOperationException(
                $"API key environment variable name is not configured for provider '{options.Provider}'.");
        }

        var apiKey = Environment.GetEnvironmentVariable(environmentVariableName);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"Environment variable '{environmentVariableName}' is not set.");
        }

        return apiKey;
    }
}