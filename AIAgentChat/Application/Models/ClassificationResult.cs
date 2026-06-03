using AIAgentChat.Application.Models;

namespace AIAgentChat.Application.Services;

public class ClassificationResult
{
    public bool Success { get; set; }
    public UserRequestClassification? Classification { get; set; }
    public string RawResponse { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
