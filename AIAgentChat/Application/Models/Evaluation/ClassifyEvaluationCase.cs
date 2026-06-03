namespace AIAgentChat.Application.Models.Evaluation;

public class ClassifyEvaluationCase
{
    public string Id { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string ExpectedCategory { get; set; } = string.Empty;
    public string ExpectedPriority { get; set; } = string.Empty;
    public string ExpectedSentiment { get; set; } = string.Empty;
    public bool ExpectedShouldEscalate { get; set; }
    public string Description { get; set; } = string.Empty;
}
