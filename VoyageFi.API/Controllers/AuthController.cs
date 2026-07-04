using Microsoft.AspNetCore.Mvc;
using VoyageFi.Core.Interfaces;

namespace VoyageFi.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly IUserRepository _userRepository;
    private readonly IRedisCacheService _redisCacheService;

    public AuthController(ITokenService tokenService, IUserRepository userRepository, IRedisCacheService redisCacheService)
    {
        _tokenService = tokenService;
        _userRepository = userRepository;
        _redisCacheService = redisCacheService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Örnek: Gerçekte şifre hash kontrolü yapılmalıdır.
        var user = await _userRepository.GetUserByEmailAsync(request.Email);
        if (user == null || user.PasswordHash != request.Password) 
            return Unauthorized("Geçersiz e-posta veya şifre.");

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateUserAsync(user);

        return Ok(new { AccessToken = accessToken, RefreshToken = refreshToken });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal == null) return BadRequest("Geçersiz token");

        var userEmail = principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(userEmail)) return BadRequest("Geçersiz token");

        var user = await _userRepository.GetUserByEmailAsync(userEmail);

        if (user == null || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            return BadRequest("Geçersiz veya süresi dolmuş refresh token.");

        var newAccessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        await _userRepository.UpdateUserAsync(user);

        return Ok(new { AccessToken = newAccessToken, RefreshToken = newRefreshToken });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        // İsteğin başlığındaki Authorization token'ı alıyoruz
        var authorizationHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
            return BadRequest("Token bulunamadı.");

        var token = authorizationHeader.Substring("Bearer ".Length).Trim();
        var principal = _tokenService.GetPrincipalFromExpiredToken(token);
        
        var jti = principal?.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
        
        if (!string.IsNullOrEmpty(jti))
        {
            // Token'ın kalan süresi kadar blacklist'te tut (Örnek: 15 dakika)
            await _redisCacheService.AddToBlacklistAsync(jti, TimeSpan.FromMinutes(15));
        }

        return Ok("Başarıyla çıkış yapıldı.");
    }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
