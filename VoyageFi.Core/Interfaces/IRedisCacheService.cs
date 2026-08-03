namespace VoyageFi.Core.Interfaces;

public interface IRedisCacheService
{
    Task AddToBlacklistAsync(string jti, TimeSpan expiration);
    Task<bool> IsBlacklistedAsync(string jti);
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value, TimeSpan expiration);
    Task RemoveAsync(string key);
}
