using Microsoft.EntityFrameworkCore;
using VoyageFi.Core.Entities;

namespace VoyageFi.Infrastructure.Data;

public class VoyageFiDbContext : DbContext
{
    public VoyageFiDbContext(DbContextOptions<VoyageFiDbContext> options) : base(options)
    {
    }

    // Veritabanında oluşacak tablolarımız
    public DbSet<User> Users { get; set; }
    public DbSet<Trip> Trips { get; set; }
    public DbSet<Expense> Expenses { get; set; }
}