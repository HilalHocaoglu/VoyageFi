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
    // Test amaçlı 15 saniye olarak güncellendi.
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

    // Uçuş verisi için kullanılacak sahte/gerçek API adresi.
    // Gerçek entegrasyonda appsettings'ten okunmalıdır.
    private const string AviationApiUrl =
        "https://api.aviationstack.com/v1/flights?access_key=YOUR_KEY&limit=10";

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
        // İlk çalışmayı 5 saniye sonraya ayarlıyoruz (Test için)
        var initialDelay = TimeSpan.FromSeconds(5);
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
        _logger.LogInformation("[{Time}] [MOCK MODE] Uçuş rötar simülasyonu başlatılıyor...", DateTime.UtcNow);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<MassTransit.IPublishEndpoint>();

        // Bu döngüde 1-3 arasında rastgele rötar eventi yayınla
        var eventCount = _random.Next(1, 4);

        for (var i = 0; i < eventCount; i++)
        {
            var mockFlightNumber   = $"TK{_random.Next(100, 9999):D4}";
            var mockPnrCode        = GenerateMockPnr();
            var mockUserId         = Guid.NewGuid().ToString();
            var mockDelayMinutes   = _random.Next(15, 240); // 15 dk – 4 saat arası rötar

            _logger.LogInformation(
                "✈️ [MOCK] Rötar eventi hazırlanıyor → Uçuş: {Flight} | PNR: {Pnr} | Rötar: {Delay} dk",
                mockFlightNumber, mockPnrCode, mockDelayMinutes);

            var flightDelayedEvent = new VoyageFi.Core.Events.FlightDelayedEvent(
                mockPnrCode,
                mockFlightNumber,
                mockUserId,
                mockDelayMinutes
            );

            await publishEndpoint.Publish(flightDelayedEvent, stoppingToken);

            _logger.LogInformation(
                "📨 [MOCK] FlightDelayedEvent → RabbitMQ'ya iletildi: {Flight} ({Delay} dk)",
                mockFlightNumber, mockDelayMinutes);

            // Eventler arasında kısa bir aralık bırak (daha gerçekçi akış görünümü için)
            await Task.Delay(TimeSpan.FromMilliseconds(300), stoppingToken);
        }

        _logger.LogInformation("[MOCK MODE] {Count} rötar eventi bu döngüde yayınlandı.", eventCount);
    }

    /// <summary>
    /// 6 karakterlik rastgele PNR kodu üretir (örn: "VF7A2K")
    /// </summary>
    private string GenerateMockPnr()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return "VF" + new string(Enumerable.Range(0, 4)
            .Select(_ => chars[_random.Next(chars.Length)])
            .ToArray());
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
