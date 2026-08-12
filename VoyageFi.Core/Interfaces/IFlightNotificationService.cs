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

    /// <summary>
    /// FAZ 2 – Adım 3: Admin komut kaydı oluşturulunca tüm istemcilere "ReceiveAuditEntry" event'i gönderir.
    /// </summary>
    Task BroadcastAuditEntryAsync(
        Guid   auditId,
        string actor,
        string command,
        string flightNumber,
        string result,
        int    durationMs,
        DateTime createdAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// FAZ 2 – Adım 4: Kriz durumunda (rötar >= 180) tüm istemcilere acil müdahale (ReceiveIncidentAlert) bildirimi gönderir.
    /// </summary>
    Task BroadcastIncidentAlertAsync(string flightNumber, string reason, int delayDurationMinutes, CancellationToken cancellationToken = default);
}
