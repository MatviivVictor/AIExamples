using System.ClientModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AIAgentChat.Application.Models;
using AIAgentChat.Application.Services;
using OllamaSharp;

using AIAgentChat.Application.Services.Evaluation;
using AIAgentChat.Application.Models.Evaluation;
using System.Diagnostics;

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

builder.Services.AddSingleton(sp =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    var embeddingGenerator = AiChatClientFactory.CreateEmbeddingGenerator(aiOptions);
    var manualPath = Path.Combine(AppContext.BaseDirectory, "Manuals", "user-guid.md");

    return new KnowledgeBaseService(manualPath, embeddingGenerator, chatClient);
});

builder.Services.AddSingleton<InputGuardrailService>();
builder.Services.AddSingleton<OutputGuardrailService>();
builder.Services.AddSingleton<RagGuardrailService>();

builder.Services.AddSingleton<ClassificationService>();
builder.Services.AddSingleton<RagService>();

builder.Services.AddSingleton<EvaluationDatasetLoader>();
builder.Services.AddSingleton<EvaluationResultWriter>();
builder.Services.AddSingleton<RagEvaluator>();
builder.Services.AddSingleton<ClassifyEvaluator>();
builder.Services.AddSingleton<GuardrailsEvaluator>();
builder.Services.AddSingleton<EvaluationRunner>();

var app = builder.Build();

var chatClient = app.Services.GetRequiredService<IChatClient>();
var inputGuardrail = app.Services.GetRequiredService<InputGuardrailService>();
var outputGuardrail = app.Services.GetRequiredService<OutputGuardrailService>();
var ragGuardrail = app.Services.GetRequiredService<RagGuardrailService>();

var classificationService = app.Services.GetRequiredService<ClassificationService>();
var ragService = app.Services.GetRequiredService<RagService>();
var evaluationRunner = app.Services.GetRequiredService<EvaluationRunner>();

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
Console.WriteLine("Type 'classify' to run structured output example.");
Console.WriteLine("Type 'docs' to ask a question using the local documentation.");
Console.WriteLine("Type 'eval' to run evaluation suites.");
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

    if (string.Equals(userPrompt, "classify", StringComparison.OrdinalIgnoreCase))
    {
        await ClassifyUserRequestAsync(classificationService);
        Console.WriteLine();
        continue;
    }

    if (string.Equals(userPrompt, "docs", StringComparison.OrdinalIgnoreCase))
    {
        await AnswerFromDocumentationAsync(ragService);
        Console.WriteLine();
        continue;
    }

    if (string.Equals(userPrompt, "eval", StringComparison.OrdinalIgnoreCase))
    {
        await RunEvaluationMenuAsync(evaluationRunner);
        Console.WriteLine();
        continue;
    }

    // Застосування Input Guardrails для звичайного чату
    var inputResult = inputGuardrail.Validate(userPrompt);
    if (!inputResult.IsAllowed)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(inputResult.UserMessage);
        Console.ResetColor();
        Console.WriteLine();
        continue;
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

        Console.WriteLine();

        // Застосування Output Guardrails після завершення стрімінгу
        var outputResult = outputGuardrail.Validate(chatResponse);
        if (outputResult.IsAllowed)
        {
            chatHistory.Add(new ChatMessage(ChatRole.Assistant, chatResponse));
        }
        else
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(outputResult.UserMessage);
            Console.ResetColor();
        }
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

static async Task ClassifyUserRequestAsync(ClassificationService classificationService)
{
    Console.WriteLine();
    Console.WriteLine("Structured output mode: user request classification.");
    Console.WriteLine("Enter text that should be classified:");
    var textToClassify = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(textToClassify)) return;

    try
    {
        Console.WriteLine();
        Console.WriteLine("Raw structured response from AI:");

        var result = await classificationService.ClassifyAsync(textToClassify);

        if (!result.Success)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(result.Error);
            Console.ResetColor();
            return;
        }

        Console.WriteLine(result.RawResponse);
        Console.WriteLine();
        PrintClassification(result.Classification!);
    }
    catch (Exception exception)
    {
        Console.WriteLine();
        Console.WriteLine("Structured output request failed.");
        Console.WriteLine(exception.Message);
    }
}

static async Task AnswerFromDocumentationAsync(RagService ragService)
{
    Console.WriteLine();
    Console.WriteLine("Documentation RAG mode.");
    Console.WriteLine("Ask a question about this project:");
    var question = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(question)) return;

    try
    {
        Console.WriteLine();
        Console.WriteLine("Answer from documentation:");
        Console.WriteLine();

        var result = await ragService.AnswerAsync(question);

        if (result.RetrievedChunks.Any())
        {
            PrintRetrievedChunks(result.RetrievedChunks);
        }

        if (result.Answered)
        {
            Console.WriteLine();
            Console.WriteLine(result.Answer);
        }
        else
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(result.CannotAnswerReason);
            Console.ResetColor();
        }
    }
    catch (Exception exception)
    {
        Console.WriteLine();
        Console.WriteLine("Documentation RAG request failed.");
        Console.WriteLine(exception.Message);
    }
}

static async Task RunEvaluationMenuAsync(EvaluationRunner evaluationRunner)
{
    while (true)
    {
        Console.WriteLine();
        Console.WriteLine("Evaluation menu:");
        Console.WriteLine("1. Run RAG evaluation");
        Console.WriteLine("2. Run classify evaluation");
        Console.WriteLine("3. Run guardrails evaluation");
        Console.WriteLine("4. Run all evaluations");
        Console.WriteLine("5. Back to chat");
        Console.Write("Select an option: ");

        var input = Console.ReadLine();

        switch (input)
        {
            case "1":
                await evaluationRunner.RunRagEvaluationAsync();
                break;
            case "2":
                await evaluationRunner.RunClassifyEvaluationAsync();
                break;
            case "3":
                await evaluationRunner.RunGuardrailsEvaluationAsync();
                break;
            case "4":
                await evaluationRunner.RunAllEvaluationsAsync();
                break;
            case "5":
                return;
            default:
                Console.WriteLine("Invalid option.");
                break;
        }
    }
}

static void PrintRetrievedChunks(IReadOnlyList<KnowledgeChunk> chunks)
{
    Console.WriteLine();
    Console.WriteLine("Retrieved documentation chunks:");

    foreach (var chunk in chunks)
    {
        Console.WriteLine($"- Source: {Path.GetFileName(chunk.Source)}, Chunk: {chunk.Index}, Score: {chunk.Score}");
    }
}

static void PrintClassification(UserRequestClassification classification)
{
    Console.WriteLine("Parsed structured result:");
    Console.WriteLine($"Category: {classification.Category}");
    Console.WriteLine($"Priority: {classification.Priority}");
    Console.WriteLine($"Sentiment: {classification.Sentiment}");
    Console.WriteLine($"Summary: {classification.Summary}");
    Console.WriteLine($"Should escalate: {classification.ShouldEscalate}");
}