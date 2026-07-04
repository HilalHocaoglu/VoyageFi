using VoyageFi.Core.Entities;
using VoyageFi.Core.Interfaces;
using VoyageFi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace VoyageFi.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly VoyageFiDbContext _context;

    public UserRepository(VoyageFiDbContext context)
    {
        _context = context;
    }

    public async Task<User> AddUserAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task UpdateUserAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }
}