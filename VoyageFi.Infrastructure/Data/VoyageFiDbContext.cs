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

    /// <summary>
    /// RAG için banka kampanyaları ve SHGM yolcu haklarını
    /// vektör embedding'leriyle birlikte tutan tablo.
    /// </summary>
    public DbSet<Campaign>      Campaigns      { get; set; }

    /// <summary>
    /// FAZ 2 – Adım 3: Her admin komutunun değişmez kayıt tablosu.
    /// </summary>
    public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PostgreSQL'de pgvector eklentisini aktif eder.
        // Bu satır olmadan vector tipli sütunlar oluşturulamaz.
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Title)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(c => c.SourceType)
                .IsRequired()
                .HasMaxLength(100);

            // EmbeddingRaw: EF Core'da string olarak tutulur.
            // Migration sonrası aşağıdaki SQL ile vector(1536) tipine dönüştürülür:
            //   ALTER TABLE "Campaigns" ALTER COLUMN "EmbeddingRaw" TYPE vector(1536) USING "EmbeddingRaw"::vector;
            // Python ingestion scripti o andan itibaren direkt pgvector formatında yazar.
            entity.Property(c => c.EmbeddingRaw)
                .HasColumnName("EmbeddingRaw")
                .HasColumnType("text");
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Bu blok SADECE EF araçları (dotnet ef migrations add) çalışırken tetiklenir.
            // Runtime'da Program.cs'deki yapılandırma kullanılır.
            optionsBuilder.UseNpgsql("Host=localhost;Database=voyagefi;Username=postgres");
        }
    }
}