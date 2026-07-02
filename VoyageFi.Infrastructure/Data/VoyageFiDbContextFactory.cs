using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VoyageFi.Infrastructure.Data;

public class VoyageFiDbContextFactory : IDesignTimeDbContextFactory<VoyageFiDbContext>
{
    public VoyageFiDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<VoyageFiDbContext>();
        
        // Güvenlik: Şifre GitHub'a sızmasın diye yerel test için sahte bir değer girilmiştir.
        optionsBuilder.UseNpgsql("Host=localhost;Database=VoyageFiDb;Username=postgres;Password=GIZLI_SIFRE");

        return new VoyageFiDbContext(optionsBuilder.Options);
    }
}