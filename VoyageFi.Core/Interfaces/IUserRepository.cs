using VoyageFi.Core.Entities;

namespace VoyageFi.Core.Interfaces;

public interface IUserRepository
{
    Task<User> AddUserAsync(User user);
    Task<User?> GetUserByEmailAsync(string email);
    Task UpdateUserAsync(User user);
}