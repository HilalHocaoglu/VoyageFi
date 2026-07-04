using Microsoft.AspNetCore.Mvc;
using VoyageFi.Application.DTOs;
using VoyageFi.Core.Entities;
using VoyageFi.Core.Interfaces;

namespace VoyageFi.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;

    public UsersController(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto request)
    {
        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = request.Password // İleride hashleyeceğiz.
        };

        var createdUser = await _userRepository.AddUserAsync(user);

        // Güvenlik: Kullanıcının şifresini dışarı dönmüyoruz!
        return Ok(new { createdUser.Id, createdUser.FirstName, createdUser.Email });
    }
}