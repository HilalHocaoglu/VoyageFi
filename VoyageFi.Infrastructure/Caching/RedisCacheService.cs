using Microsoft.Extensions.Caching.Distributed;
using VoyageFi.Core.Interfaces;

namespace VoyageFi.Infrastructure.Caching;

public class RedisCacheService : IRedisCacheService
{
    private readonly IDistributedCache _cache;

    public RedisCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task AddToBlacklistAsync(string jti, TimeSpan expiration)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        };

        await _cache.SetStringAsync($"blacklist:{jti}", "revoked", options);
    }

    public async Task<bool> IsBlacklistedAsync(string jti)
    {
        var value = await _cache.GetStringAsync($"blacklist:{jti}");
        return !string.IsNullOrEmpty(value);
    }
}
