namespace AIAgentChat.Application.Models;

public sealed class GuardrailResult
{
    public bool IsAllowed { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string UserMessage { get; init; } = string.Empty;
    public GuardrailRiskLevel RiskLevel { get; init; }
    public List<string> TriggeredRules { get; init; } = [];

    public static GuardrailResult Success() => new() { IsAllowed = true };

    public static GuardrailResult Failure(string reason, string userMessage, GuardrailRiskLevel riskLevel, List<string> triggeredRules) =>
        new()
        {
            IsAllowed = false,
            Reason = reason,
            UserMessage = userMessage,
            RiskLevel = riskLevel,
            TriggeredRules = triggeredRules
        };
}
