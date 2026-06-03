using AIAgentChat.Application.Models;

namespace AIAgentChat.Application.Models.Evaluation;

public class EvaluationRunResult
{
    public string RunId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public List<EvaluationSuiteResult> Suites { get; set; } = [];
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public double PassRate { get; set; }
}
