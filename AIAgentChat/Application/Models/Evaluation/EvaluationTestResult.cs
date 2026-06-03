namespace AIAgentChat.Application.Models.Evaluation;

public class EvaluationTestResult
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string FailureReason { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string RawOutput { get; set; } = string.Empty;
}
