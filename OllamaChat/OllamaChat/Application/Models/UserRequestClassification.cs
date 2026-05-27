namespace OllamaChat.Application.Models;

/// <summary>
/// Represents a structured AI response for user request classification.
/// 
/// This class is intentionally simple:
/// - strings are used for category/priority/sentiment to keep the example provider-agnostic;
/// - bool is used for escalation decision because the application can use it directly.
/// 
/// In a production system, you could replace strings with enums and add stricter validation.
/// </summary>
internal sealed class UserRequestClassification
{
    /// <summary>
    /// High-level category of the user's request.
    /// Example values: TechnicalSupport, Billing, Sales, GeneralQuestion, Unknown.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Priority of the request.
    /// Example values: Low, Medium, High, Critical.
    /// </summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>
    /// Detected sentiment of the user's message.
    /// Example values: Positive, Neutral, Negative.
    /// </summary>
    public string Sentiment { get; set; } = string.Empty;

    /// <summary>
    /// Short summary of the user's message.
    /// This should be concise and suitable for displaying in logs, tickets, or dashboards.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether this request should be escalated to a human/operator.
    /// </summary>
    public bool ShouldEscalate { get; set; }

    /// <summary>
    /// Validates the minimum required fields.
    /// 
    /// We do not trust the AI blindly. Even if we asked for JSON,
    /// the model can still return incomplete or malformed data.
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Category)
               && !string.IsNullOrWhiteSpace(Priority)
               && !string.IsNullOrWhiteSpace(Sentiment)
               && !string.IsNullOrWhiteSpace(Summary);
    }
}