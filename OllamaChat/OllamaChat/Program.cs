using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OllamaChat.Application.Models;
using OllamaSharp;

var builder = Host.CreateApplicationBuilder(args);

var aiOptions = builder.Configuration
    .GetSection("Ai")
    .Get<AiOptions>()
    ?? throw new InvalidOperationException("AI configuration section is missing.");

if (!string.Equals(aiOptions.Provider, "Ollama", StringComparison.OrdinalIgnoreCase))
{
    throw new NotSupportedException($"Provider '{aiOptions.Provider}' is not supported yet.");
}

if (string.IsNullOrWhiteSpace(aiOptions.Endpoint))
{
    throw new InvalidOperationException("AI endpoint is not configured.");
}

if (string.IsNullOrWhiteSpace(aiOptions.Model))
{
    throw new InvalidOperationException("AI model is not configured.");
}

builder.Services.AddChatClient(
    new OllamaApiClient(new Uri(aiOptions.Endpoint), aiOptions.Model));

var app = builder.Build();

var chatClient = app.Services.GetRequiredService<IChatClient>();

var chatHistory = new List<ChatMessage>();

if (!string.IsNullOrWhiteSpace(aiOptions.SystemPrompt))
{
    chatHistory.Add(new ChatMessage(ChatRole.System, aiOptions.SystemPrompt));
}

Console.WriteLine("AI chat is ready.");
Console.WriteLine($"Provider: {aiOptions.Provider}");
Console.WriteLine($"Model: {aiOptions.Model}");
Console.WriteLine("Type your prompt and press Enter.");
Console.WriteLine("Type 'exit' to quit.");
Console.WriteLine();

while (true)
{
    Console.WriteLine("Enter your prompt:");
    var userPrompt = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(userPrompt))
    {
        Console.WriteLine("Prompt cannot be empty.");
        Console.WriteLine();
        continue;
    }

    if (string.Equals(userPrompt, "exit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Goodbye.");
        break;
    }

    chatHistory.Add(new ChatMessage(ChatRole.User, userPrompt));

    Console.WriteLine("Response from AI:");

    var chatResponse = "";

    await foreach (var item in chatClient.GetStreamingResponseAsync(chatHistory))
    {
        Console.Write(item.Text);
        chatResponse += item.Text;
    }

    chatHistory.Add(new ChatMessage(ChatRole.Assistant, chatResponse));

    Console.WriteLine();
    Console.WriteLine();
}