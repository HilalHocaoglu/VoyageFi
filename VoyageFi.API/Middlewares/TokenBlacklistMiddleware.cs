using Microsoft.AspNetCore.Authorization;
using VoyageFi.Core.Interfaces;

namespace VoyageFi.API.Middlewares;

public class TokenBlacklistMiddleware
{
    private readonly RequestDelegate _next;

    public TokenBlacklistMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IRedisCacheService redisCacheService)
    {
        // Eğer endpoint [Authorize] gerektirmiyorsa veya token yoksa devam et
        var endpoint = context.GetEndpoint();
        var isAuthorized = endpoint?.Metadata?.GetMetadata<IAuthorizeData>() != null;

        if (isAuthorized)
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                await _next(context);
                return;
            }
            var jti = context.User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
            if (!string.IsNullOrEmpty(jti))
            {
                var isBlacklisted = await redisCacheService.IsBlacklistedAsync(jti);
                if (isBlacklisted)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"Message\": \"Oturumunuz sonlandırılmış (Token Blacklisted). Lütfen tekrar giriş yapın.\"}");
                    return;
                }
            }
        }

        await _next(context);
    }
}
