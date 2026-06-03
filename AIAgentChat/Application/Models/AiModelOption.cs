namespace AIAgentChat.Application.Models;

public sealed class AiModelOption
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string SettingsFile { get; init; } = string.Empty;
}