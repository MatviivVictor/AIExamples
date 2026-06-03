namespace AIAgentChat.Application.Models.Evaluation;

public class RagEvaluationCase
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public List<string> ExpectedAnswerContains { get; set; } = [];
    public string ExpectedSource { get; set; } = string.Empty;
    public bool ShouldAnswer { get; set; }
    public string Description { get; set; } = string.Empty;
}
