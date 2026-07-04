using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VoyageFi.Infrastructure.Data;

public class VoyageFiDbContextFactory : IDesignTimeDbContextFactory<VoyageFiDbContext>
{
    public VoyageFiDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<VoyageFiDbContext>();
        
        // Güvenlik: Yerel test için Docker konteynerine (Port 5433) bağlanır.
        optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=VoyageFiDb;Username=postgres;Password=supergizlisifre");

        return new VoyageFiDbContext(optionsBuilder.Options);
    }
}