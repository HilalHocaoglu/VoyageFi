namespace VoyageFi.Core.Interfaces;

public interface IRedisCacheService
{
    Task AddToBlacklistAsync(string jti, TimeSpan expiration);
    Task<bool> IsBlacklistedAsync(string jti);
}
