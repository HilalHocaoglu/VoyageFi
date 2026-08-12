using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using VoyageFi.API.Hubs;
using VoyageFi.Core.Interfaces;

namespace VoyageFi.API.Services;

/// <summary>
/// IFlightNotificationService'in SignalR implementasyonu.
/// FlightMonitorHub aracılığıyla Admin Dashboard'a gerçek zamanlı bildirim gönderir.
/// </summary>
public class SignalRFlightNotificationService : IFlightNotificationService
{
    private readonly IHubContext<FlightMonitorHub> _hubContext;
    private readonly ILogger<SignalRFlightNotificationService> _logger;

    public SignalRFlightNotificationService(
        IHubContext<FlightMonitorHub> hubContext,
        ILogger<SignalRFlightNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastFlightDelayAsync(
        string pnrCode,
        string flightNumber,
        string userId,
        int delayDurationMinutes,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "📡 [SignalR] Rötar bildirimi yayınlanıyor: {FlightNumber} (PNR: {PnrCode}) → {Delay} dakika",
            flightNumber, pnrCode, delayDurationMinutes);

        await _hubContext.Clients.All.SendAsync(
            "ReceiveFlightDelay",
            new
            {
                PnrCode = pnrCode,
                FlightNumber = flightNumber,
                UserId = userId,
                DelayDurationMinutes = delayDurationMinutes,
                Timestamp = DateTime.UtcNow
            },
            cancellationToken);
    }

    public async Task BroadcastAuditEntryAsync(
        Guid auditId,
        string actor,
        string command,
        string flightNumber,
        string result,
        int durationMs,
        DateTime createdAt,
        CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.All.SendAsync(
            "ReceiveAuditEntry",
            new
            {
                auditId = auditId,
                actor = actor,
                command = command,
                flightNumber = flightNumber,
                result = result,
                durationMs = durationMs,
                createdAt = createdAt
            },
            cancellationToken);
    }
    public async Task BroadcastIncidentAlertAsync(
        string flightNumber,
        string reason,
        int delayDurationMinutes,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "🚨 [SignalR] Kriz Alarmı (Incident Alert) yayınlanıyor: {FlightNumber} | Sebep: {Reason}",
            flightNumber, reason);

        await _hubContext.Clients.All.SendAsync(
            "ReceiveIncidentAlert",
            new
            {
                FlightNumber = flightNumber,
                Reason = reason,
                DelayDurationMinutes = delayDurationMinutes,
                Timestamp = DateTime.UtcNow
            },
            cancellationToken);
    }
}
