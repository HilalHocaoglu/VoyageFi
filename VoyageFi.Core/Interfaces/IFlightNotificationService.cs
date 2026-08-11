namespace VoyageFi.Core.Interfaces;

/// <summary>
/// Uçuş rötar bildirimlerini gerçek zamanlı olarak bağlı istemcilere iletmek için soyutlama.
/// Bu interface, Infrastructure katmanının SignalR'a doğrudan bağımlı olmamasını sağlar.
/// </summary>
public interface IFlightNotificationService
{
    /// <summary>
    /// Bağlı tüm istemcilere (Admin Dashboard) uçuş rötar bilgisini anlık olarak gönderir.
    /// </summary>
    Task BroadcastFlightDelayAsync(string pnrCode, string flightNumber, string userId, int delayDurationMinutes, CancellationToken cancellationToken = default);
}
