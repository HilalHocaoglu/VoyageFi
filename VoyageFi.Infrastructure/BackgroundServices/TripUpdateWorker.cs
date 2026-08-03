using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VoyageFi.Infrastructure.Data;

namespace VoyageFi.Infrastructure.BackgroundServices;

/// <summary>
/// Uçuş durum bilgilerini gerçek zamanlı olarak takip eden arka plan servisi.
/// 
/// Mimari Not:
/// BackgroundService Singleton yaşam döngüsüyle çalışır. Ancak DbContext
/// Scoped'dır. Bu nedenle DbContext'e doğrudan enjeksiyon YAPAMAYIZ.
/// Çözüm: IServiceScopeFactory enjekte ederek her tetiklenişte
/// kısa ömürlü bir scope açıp DbContext'i güvenle kullanırız.
/// </summary>
public class TripUpdateWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TripUpdateWorker> _logger;
    private readonly HttpClient _httpClient;

    // Kaç dakikada bir kontrol yapılacağını buradan ayarlayın.
    // Production için 1 saat olarak güncellendi.
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    // Uçuş verisi için kullanılacak sahte/gerçek API adresi.
    // Gerçek entegrasyonda appsettings'ten okunmalıdır.
    private const string AviationApiUrl =
        "https://api.aviationstack.com/v1/flights?access_key=YOUR_KEY&limit=10";

    // Rastgele durum ataması için olası değerler
    private static readonly string[] PossibleStatuses = ["OnTime", "Delayed", "Cancelled"];
    private readonly Random _random = new();

    public TripUpdateWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<TripUpdateWorker> logger,
        IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("AviationApi");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // İlk çalışmayı 1 saat sonraya ayarlıyoruz
        var initialDelay = TimeSpan.FromHours(1);
        _logger.LogInformation("TripUpdateWorker başlatıldı. İlk kontrol {Delay} sonra yapılacak.", initialDelay);
            
        await Task.Delay(initialDelay, stoppingToken);

        // Uygulama kapanana kadar döner
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken);

            try
            {
                await CheckAndUpdateTripsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Uygulama kapatılırken fırlatılan beklenen exception — log'lamıyoruz
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "TripUpdateWorker bir hatayla karşılaştı. Bir sonraki döngüde tekrar denecek.");
            }
        }

        _logger.LogInformation("TripUpdateWorker durdu.");
    }

    private async Task CheckAndUpdateTripsAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[{Time}] Uçuş durumları kontrol ediliyor...", DateTime.UtcNow);

        // 1. Sahte/gerçek API'yi çağır
        //    Gerçek entegrasyonda bu response parse edilip PNR'a göre eşleşme yapılır.
        //    Şimdilik yanıtı kullanmıyoruz; mantığı simüle ediyoruz.
        await CallAviationApiAsync(stoppingToken);

        // 2. Kısa ömürlü scope ile DbContext ve RedisCacheService'i güvenle kullan
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VoyageFiDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<VoyageFi.Core.Interfaces.IRedisCacheService>();

        // 3. Bugün kalkan aktif uçuşları al
        var today = DateTime.UtcNow.Date;
        var cacheKey = $"trips:ontime:{today:yyyyMMdd}";
        
        List<VoyageFi.Core.Entities.Trip>? trips = null;
        var cachedTrips = await cache.GetAsync(cacheKey);
        
        if (!string.IsNullOrEmpty(cachedTrips))
        {
            _logger.LogInformation("Aktif uçuşlar Redis Cache üzerinden getirildi.");
            trips = System.Text.Json.JsonSerializer.Deserialize<List<VoyageFi.Core.Entities.Trip>>(cachedTrips);
        }
        else
        {
            _logger.LogInformation("Aktif uçuşlar veritabanından çekiliyor...");
            trips = await db.Trips
                .Where(t => t.DepartureTime.Date == today && t.Status == "OnTime")
                .ToListAsync(stoppingToken);
                
            if (trips != null && trips.Any())
            {
                var serializedTrips = System.Text.Json.JsonSerializer.Serialize(trips);
                await cache.SetAsync(cacheKey, serializedTrips, TimeSpan.FromMinutes(10));
            }
        }

        if (trips == null || !trips.Any())
        {
            _logger.LogInformation("Bugün için güncellenecek aktif uçuş bulunamadı.");
            return;
        }

        // 4. Simülasyon: Her uçuşa rastgele durum ata
        //    (Gerçek entegrasyonda API yanıtındaki veriye göre yapılır)
        var updatedCount = 0;
        foreach (var trip in trips)
        {
            var newStatus = PossibleStatuses[_random.Next(PossibleStatuses.Length)];
            if (trip.Status == newStatus) continue;

            var oldStatus = trip.Status;
            trip.Status = newStatus;
            
            // Eğer Redis'ten geldiyse EF Core track etmiyor olabilir. Attach edip güncelleyelim.
            db.Update(trip);
            
            updatedCount++;

            _logger.LogInformation(
                "Trip {TripId} | PNR: {Pnr} → {OldStatus} → {NewStatus}",
                trip.Id, trip.PnrCode, oldStatus, newStatus);
        }

        // 5. Değişiklikleri kaydet
        if (updatedCount > 0)
        {
            await db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("{Count} uçuşun durumu güncellendi.", updatedCount);
        }
        else
        {
            _logger.LogInformation("Durumu değişen uçuş yok.");
        }
    }

    private async Task CallAviationApiAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Gerçek API anahtarı olmadan 401/403 alınır — bu normaldir.
            // Gerçek entegrasyonda buraya response.Content.ReadAsStringAsync() ekleyin
            // ve JSON'u parse ederek Trip.Status'u API verisine göre güncelleyin.
            var response = await _httpClient.GetAsync(AviationApiUrl, stoppingToken);

            _logger.LogDebug(
                "AviationStack API yanıtı: {StatusCode}", response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            // API erişilemez durumdayken bile worker çalışmaya devam eder
            _logger.LogWarning(
                "AviationStack API'ye ulaşılamadı: {Message}. Simülasyon modu devrede.", ex.Message);
        }
    }
}
