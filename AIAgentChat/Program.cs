using System.ClientModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AIAgentChat.Application.Models;
using AIAgentChat.Application.Services;

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

var manualPath = Path.Combine(AppContext.BaseDirectory, "Manuals", "user-guid.md");
var knowledgeBase = new KnowledgeBaseService(manualPath);

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
        await ClassifyUserRequestAsync(chatClient);
        Console.WriteLine();
        continue;
    }

    if (string.Equals(userPrompt, "docs", StringComparison.OrdinalIgnoreCase))
    {
        await AnswerFromDocumentationAsync(chatClient, knowledgeBase);
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

static async Task ClassifyUserRequestAsync(IChatClient chatClient)
{
    Console.WriteLine();
    Console.WriteLine("Structured output mode: user request classification.");
    Console.WriteLine("Enter text that should be classified:");
    var textToClassify = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(textToClassify))
    {
        Console.WriteLine("Text cannot be empty.");
        return;
    }

    // This prompt asks the model to return JSON only.
    //
    // Important:
    // We explicitly describe the required JSON shape because different providers
    // may not support the same native structured-output features.
    //
    // This approach is provider-agnostic:
    // - it can work with Ollama;
    // - it can work with OpenAI;
    // - it can work with Gemini;
    // - it does not require provider-specific response_format APIs.
    //
    // Limitation:
    // The model can still return invalid JSON, so the application must validate it.
    var classificationPrompt = $$"""
        You are a request classification engine.

        Classify the user's message and return ONLY valid JSON.
        Do not include markdown.
        Do not include explanations.
        Do not wrap the JSON in ```json blocks.

        The JSON must have exactly this shape:
        {
          "category": "TechnicalSupport | Billing | Sales | GeneralQuestion | Unknown",
          "priority": "Low | Medium | High | Critical",
          "sentiment": "Positive | Neutral | Negative",
          "summary": "Short one-sentence summary",
          "shouldEscalate": true
        }

        Rules:
        - Use "TechnicalSupport" for errors, bugs, crashes, setup problems, API issues, and integration problems.
        - Use "Billing" for payments, invoices, subscription, quota, or pricing issues.
        - Use "Sales" for buying, product comparison, or commercial questions.
        - Use "GeneralQuestion" for simple questions that are not support, billing, or sales.
        - Use "Unknown" if the message cannot be classified.
        - Use "Critical" only when there is production outage, data loss, security issue, or blocked business-critical workflow.
        - shouldEscalate must be true for Critical priority or when a human should review the request.

        User message:
        {{textToClassify}}
        """;

    var messages = new List<ChatMessage>
    {
        // This system message scopes the model behavior for this operation only.
        // It is intentionally separate from normal chat history, because classification
        // should not be influenced by previous casual conversation.
        new(ChatRole.System, "You produce strict machine-readable JSON for application processing."),

        // The user message contains the actual classification task.
        new(ChatRole.User, classificationPrompt)
    };

    try
    {
        Console.WriteLine();
        Console.WriteLine("Raw structured response from AI:");

        var rawResponse = await ReadStreamingTextAsync(chatClient, messages);

        Console.WriteLine();
        Console.WriteLine();

        if (!TryParseClassification(rawResponse, out var classification, out var error))
        {
            Console.WriteLine("Cannot process AI structured response.");
            Console.WriteLine(error);
            return;
        }

        PrintClassification(classification);
    }
    catch (ClientResultException exception) when (exception.Status == 429)
    {
        Console.WriteLine();
        Console.WriteLine("AI provider returned HTTP 429.");
        Console.WriteLine("This usually means quota or rate limit problem.");
        Console.WriteLine("Try again later or choose another model.");
    }
    catch (Exception exception)
    {
        Console.WriteLine();
        Console.WriteLine("Structured output request failed.");
        Console.WriteLine(exception.Message);
    }
}

static async Task<string> ReadStreamingTextAsync(
    IChatClient chatClient,
    IReadOnlyList<ChatMessage> messages)
{
    var response = "";

    // We still use streaming here, even for structured output.
    //
    // This demonstrates that structured output and streaming can be combined:
    // - the user sees the response as it arrives;
    // - the application still collects the full response;
    // - after streaming is complete, the application parses the final JSON.
    await foreach (var item in chatClient.GetStreamingResponseAsync(messages))
    {
        Console.Write(item.Text);
        response += item.Text;
    }

    return response;
}

static bool TryParseClassification(
    string rawResponse,
    out UserRequestClassification classification,
    out string error)
{
    classification = new UserRequestClassification();
    error = string.Empty;

    if (string.IsNullOrWhiteSpace(rawResponse))
    {
        error = "AI returned an empty response.";
        return false;
    }

    // Some models may still return markdown despite instructions.
    // This small cleanup makes the demo more tolerant.
    //
    // In production, you may prefer to reject such responses instead of cleaning them.
    var cleanedResponse = rawResponse
        .Replace("```json", "", StringComparison.OrdinalIgnoreCase)
        .Replace("```", "", StringComparison.OrdinalIgnoreCase)
        .Trim();

    try
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var parsed = JsonSerializer.Deserialize<UserRequestClassification>(
            cleanedResponse,
            jsonOptions);

        if (parsed is null)
        {
            error = "AI response could not be parsed into the expected object.";
            return false;
        }

        if (!parsed.IsValid())
        {
            error = "AI response JSON is valid, but required fields are missing or empty.";
            return false;
        }

        classification = parsed;
        return true;
    }
    catch (JsonException exception)
    {
        error = $"AI response is not valid JSON: {exception.Message}";
        return false;
    }
}

// ... existing code ...

static async Task AnswerFromDocumentationAsync(
    IChatClient chatClient,
    KnowledgeBaseService knowledgeBase)
{
    Console.WriteLine();
    Console.WriteLine("Documentation RAG mode.");
    Console.WriteLine("Ask a question about this project:");
    var question = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(question))
    {
        Console.WriteLine("Question cannot be empty.");
        return;
    }

    IReadOnlyList<KnowledgeChunk> chunks;

    try
    {
        chunks = knowledgeBase.Search(question, maxResults: 3);
    }
    catch (FileNotFoundException exception)
    {
        Console.WriteLine();
        Console.WriteLine("Knowledge base file was not found.");
        Console.WriteLine(exception.Message);
        return;
    }

    if (chunks.Count == 0)
    {
        Console.WriteLine();
        Console.WriteLine("Cannot answer from documentation.");
        Console.WriteLine("No relevant information was found in Manuals/user-guid.md.");
        return;
    }

    PrintRetrievedChunks(chunks);

    var ragPrompt = BuildRagPrompt(question, chunks);

    var messages = new List<ChatMessage>
    {
        new(
            ChatRole.System,
            """
            You are a documentation assistant for this console AI chat project.

            Your task:
            - read the provided documentation context;
            - answer the user's question using that context;
            - explain practical steps when the context contains them.

            Refuse to answer only if the provided context is not related to the question.
            """),
        new(ChatRole.User, ragPrompt)
    };

    try
    {
        Console.WriteLine();
        Console.WriteLine("Answer from documentation:");
        Console.WriteLine();

        await foreach (var item in chatClient.GetStreamingResponseAsync(messages))
        {
            Console.Write(item.Text);
        }

        Console.WriteLine();
    }
    catch (ClientResultException exception) when (exception.Status == 429)
    {
        Console.WriteLine();
        Console.WriteLine("AI provider returned HTTP 429.");
        Console.WriteLine("This usually means quota or rate limit problem.");
        Console.WriteLine("Try again later, reduce prompt size, or choose another model.");
    }
    catch (Exception exception)
    {
        Console.WriteLine();
        Console.WriteLine("Documentation RAG request failed.");
        Console.WriteLine(exception.Message);
    }
}

static string BuildRagPrompt(
    string question,
    IReadOnlyList<KnowledgeChunk> chunks)
{
    var context = string.Join(
        "\n\n--- DOCUMENT CHUNK ---\n\n",
        chunks.Select(chunk =>
            $"""
             Source file: {Path.GetFileName(chunk.Source)}
             Chunk number: {chunk.Index}
             Relevance score: {chunk.Score}

             {chunk.Content}
             """));

    return $$"""
             You must answer the user's question using the documentation context below.

             Important rules:
             - The documentation context is the only source of truth.
             - If the context contains relevant information, answer the question directly.
             - Do not say "Cannot answer" when the context contains useful instructions.
             - Say "Cannot answer from the provided documentation." only when the context is unrelated to the question.
             - Keep the answer practical and concise.
             - If useful, mention the source file name.

             Documentation context:
             {{context}}

             User question:
             {{question}}

             Answer:
             """;
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