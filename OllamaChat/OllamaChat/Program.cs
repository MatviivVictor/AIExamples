using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OllamaChat.Application.Models;

var builder = Host.CreateApplicationBuilder(args);

var availableModels = builder.Configuration
    .GetSection("AiModels")
    .Get<List<AiModelOption>>()
    ?? [];

if (availableModels.Count == 0)
{
    throw new InvalidOperationException("No AI models are configured in appsettings.json.");
}

var selectedModel = SelectAiModel(availableModels);

var aiOptions = LoadAiOptions(selectedModel);
aiOptions.Validate();

builder.Services.AddSingleton(aiOptions);
builder.Services.AddChatClient(_ => AiChatClientFactory.Create(aiOptions));

var app = builder.Build();

var chatClient = app.Services.GetRequiredService<IChatClient>();

var chatHistory = new List<ChatMessage>();

if (!string.IsNullOrWhiteSpace(aiOptions.SystemPrompt))
{
    chatHistory.Add(new ChatMessage(ChatRole.System, aiOptions.SystemPrompt));
}

Console.WriteLine();
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

    try
    {
        await foreach (var item in chatClient.GetStreamingResponseAsync(chatHistory))
        {
            Console.Write(item.Text);
            chatResponse += item.Text;
        }

        chatHistory.Add(new ChatMessage(ChatRole.Assistant, chatResponse));
    }
    catch (ClientResultException exception) when (exception.Status == 429)
    {
        chatHistory.RemoveAt(chatHistory.Count - 1);

        Console.WriteLine();
        Console.WriteLine($"AI provider '{aiOptions.Provider}' returned HTTP 429.");

        if (aiOptions.IsOpenAI())
        {
            Console.WriteLine("This usually means OpenAI quota, billing, or rate limit problem.");
            Console.WriteLine("Check OpenAI billing, usage limits, project limits, and API key.");
        }
        else if (aiOptions.IsGemini())
        {
            Console.WriteLine("This usually means Gemini quota or rate limit problem.");
            Console.WriteLine("Check Google AI Studio / Google Cloud quota, API key restrictions, and rate limits.");
        }
        else
        {
            Console.WriteLine("This usually means provider quota or rate limit problem.");
        }

        Console.WriteLine("You can wait and retry later, reduce prompt size, or choose another model.");
    }
    catch (Exception exception)
    {
        chatHistory.RemoveAt(chatHistory.Count - 1);

        Console.WriteLine();
        Console.WriteLine("AI request failed.");
        Console.WriteLine(exception.Message);
    }
    
    Console.WriteLine();
    Console.WriteLine();
}

static AiModelOption SelectAiModel(IReadOnlyList<AiModelOption> availableModels)
{
    Console.WriteLine("Select AI language model:");
    Console.WriteLine();

    for (var index = 0; index < availableModels.Count; index++)
    {
        var model = availableModels[index];

        Console.WriteLine($"{index + 1}. {model.DisplayName}");
        Console.WriteLine($"   Provider: {model.Provider}");
        Console.WriteLine($"   Model: {model.Model}");
        Console.WriteLine();
    }

    while (true)
    {
        Console.Write("Enter model number: ");
        var input = Console.ReadLine();

        if (int.TryParse(input, out var selectedNumber)
            && selectedNumber >= 1
            && selectedNumber <= availableModels.Count)
        {
            return availableModels[selectedNumber - 1];
        }

        Console.WriteLine("Invalid selection. Please enter a valid model number.");
        Console.WriteLine();
    }
}

static AiOptions LoadAiOptions(AiModelOption selectedModel)
{
    if (string.IsNullOrWhiteSpace(selectedModel.SettingsFile))
    {
        throw new InvalidOperationException(
            $"Settings file is not configured for model '{selectedModel.DisplayName}'.");
    }

    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile(selectedModel.SettingsFile, optional: false, reloadOnChange: false)
        .Build();

    var aiOptions = configuration
        .GetSection("Ai")
        .Get<AiOptions>()
        ?? throw new InvalidOperationException(
            $"AI configuration section is missing in '{selectedModel.SettingsFile}'.");

    aiOptions.Provider = selectedModel.Provider;
    aiOptions.Model = selectedModel.Model;

    return aiOptions;
}