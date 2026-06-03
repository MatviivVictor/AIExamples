namespace AIAgentChat.Application.Models.Evaluation;

public class GuardrailsEvaluationCase
{
    public string Id { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public bool ExpectedIsAllowed { get; set; }
    public string ExpectedRiskLevel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
