using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AIAgentChat.Application.Models;
using AIAgentChat.Application.Utilities;

namespace AIAgentChat.Application.Services.Caching;

public class AppCacheService
{
    private readonly IMemoryCache _cache;
    private readonly CacheOptions _options;
    private readonly ILogger<AppCacheService> _logger;

    public AppCacheService(
        IMemoryCache cache,
        CacheOptions options,
        ILogger<AppCacheService> logger)
    {
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Cache is disabled. Skipping TryGet for key: {Key}", key);
            value = default;
            return false;
        }

        if (_cache.TryGetValue(key, out value))
        {
            _logger.LogInformation("Cache HIT for key: {Key}", key);
            return true;
        }

        _logger.LogInformation("Cache MISS for key: {Key}", key);
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan ttl)
    {
        if (!_options.Enabled)
        {
            return;
        }

        _cache.Set(key, value, ttl);
        _logger.LogInformation("Cache SET for key: {Key} with TTL: {Ttl}", key, ttl);
    }

    public string BuildKey(params string[] parts)
    {
        var combined = string.Join(":", parts.Select(p => p.ToLowerInvariant()));
        // Use SHA256 to avoid storing raw prompts in keys and to keep keys at a reasonable length
        return SafeLogText.CreateSha256Hash(combined);
    }
}
