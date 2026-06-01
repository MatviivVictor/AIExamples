using System.Text.RegularExpressions;
using AIAgentChat.Application.Models;

namespace AIAgentChat.Application.Services;

internal sealed partial class OutputGuardrailService
{
    [GeneratedRegex(@"(?i)(api[_-]?key|password|secret|token)[\s:=]+[a-z0-9\-_]{16,}", RegexOptions.Compiled)]
    private static partial Regex SecretRegex();

    private readonly string[] _forbiddenPatterns =
    [
        "BEGIN PRIVATE KEY",
        "password:",
        "token:",
        "secret:",
        "api_key:"
    ];

    /// <summary>
    /// Валідує відповідь ШІ перед тим як показати її користувачу або зберегти в історію.
    /// AI output може містити галюцинації або випадково розкрити секрети (наприклад, з системного промпту).
    /// </summary>
    public GuardrailResult Validate(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return GuardrailResult.Success();
        }

        var triggeredRules = new List<string>();

        // Перевірка на явні заборонені фрази
        foreach (var pattern in _forbiddenPatterns)
        {
            if (output.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                triggeredRules.Add($"ForbiddenPattern:{pattern}");
            }
        }

        // Перевірка регулярним виразом на ключоподібні структури
        if (SecretRegex().IsMatch(output))
        {
            triggeredRules.Add("PotentialSecretExposed");
        }

        if (triggeredRules.Count > 0)
        {
            return GuardrailResult.Failure(
                "AI output contains sensitive information",
                "Відповідь ШІ була заблокована, оскільки вона може містити конфіденційні дані (паролі або ключі).",
                GuardrailRiskLevel.High,
                triggeredRules);
        }

        return GuardrailResult.Success();
    }
}
