using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AIAgentChat.Application.Utilities;

public static class SafeLogText
{
    private static readonly Regex ApiKeyRegex = new(@"((?:api[_-]?)?key|secret|token|password|auth|credential)[^a-z0-9]{1,3}([a-z0-9-_]{8,})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string CreatePreview(string? text, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        
        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (normalized.Length <= maxLength) return normalized;
        
        return normalized[..maxLength] + "...";
    }

    public static string CreateSha256Hash(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "empty";
        
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string MaskPotentialSecrets(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        return ApiKeyRegex.Replace(text, m => 
        {
            var label = m.Groups[1].Value;
            var separator = text.Substring(m.Groups[1].Index + m.Groups[1].Length, m.Groups[2].Index - (m.Groups[1].Index + m.Groups[1].Length));
            var secret = m.Groups[2].Value;
            
            if (secret.Length <= 4) return m.Value;
            
            return $"{label}{separator}{secret[..2]}***{secret[^2..]}";
        });
    }
}
