using System.Security.Claims;
using VoyageFi.Core.Entities;

namespace VoyageFi.Core.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
