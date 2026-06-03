namespace AIAgentChat.Application.Models.Evaluation;

public class EvaluationSuiteResult
{
    public string SuiteName { get; set; } = string.Empty;
    public List<EvaluationTestResult> Tests { get; set; } = [];
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public double PassRate { get; set; }
}
