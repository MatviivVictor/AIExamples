using Microsoft.Extensions.AI;
using OllamaChat.Application.Models;
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

    private static IChatClient CreateOllamaClient(AiOptions options)
    {
        return new OllamaApiClient(new Uri(options.Endpoint), options.Model);
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