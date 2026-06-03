using AIAgentChat.Application.Models;

namespace AIAgentChat.Application.Services;

public sealed class InputGuardrailService
{
    private const int MaxInputLength = 2000;

    private readonly string[] _injectionPatterns =
    [
        "ignore previous instructions",
        "ignore all instructions",
        "reveal system prompt",
        "show me your system prompt",
        "developer message",
        "system message",
        "jailbreak",
        "act as DAN",
        "bypass safety",
        "forget your rules"
    ];

    private readonly string[] _secretPatterns =
    [
        "api key",
        "password",
        "token",
        "secret",
        "private key",
        "environment variable value"
    ];

    private readonly string[] _dangerousPatterns =
    [
        "read system files",
        "bypass billing",
        "bypass quota",
        "hide from logging",
        "disable logs"
    ];

    /// <summary>
    /// Перевіряє вхідні дані користувача на безпеку.
    /// User input не можна довіряти, оскільки він може містити спроби зламу (prompt injection).
    /// </summary>
    public GuardrailResult Validate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return GuardrailResult.Failure(
                "Empty input",
                "Запит не може бути порожнім.",
                GuardrailRiskLevel.Low,
                ["EmptyOrWhitespace"]);
        }

        if (input.Length > MaxInputLength)
        {
            return GuardrailResult.Failure(
                "Input too long",
                $"Запит занадто довгий. Максимальна довжина: {MaxInputLength} символів.",
                GuardrailRiskLevel.Low,
                ["MaxLengthExceeded"]);
        }

        var triggeredRules = new List<string>();
        var normalizedInput = input.ToLowerInvariant();

        // Перевірка на Prompt Injection
        foreach (var pattern in _injectionPatterns)
        {
            if (normalizedInput.Contains(pattern))
            {
                triggeredRules.Add($"PromptInjection:{pattern}");
            }
        }

        // Перевірка на запити секретів
        foreach (var pattern in _secretPatterns)
        {
            if (normalizedInput.Contains(pattern))
            {
                triggeredRules.Add($"SecretAccess:{pattern}");
            }
        }

        // Перевірка на небезпечні дії
        foreach (var pattern in _dangerousPatterns)
        {
            if (normalizedInput.Contains(pattern))
            {
                triggeredRules.Add($"DangerousAction:{pattern}");
            }
        }

        if (triggeredRules.Count > 0)
        {
            var riskLevel = triggeredRules.Any(r => r.StartsWith("PromptInjection")) 
                ? GuardrailRiskLevel.High 
                : GuardrailRiskLevel.Medium;

            return GuardrailResult.Failure(
                "Safety rules triggered",
                "Запит заблоковано правилами безпеки. Я не можу допомагати з отриманням секретів, обходом правил або виконанням небезпечних команд.",
                riskLevel,
                triggeredRules);
        }

        return GuardrailResult.Success();
    }
}
