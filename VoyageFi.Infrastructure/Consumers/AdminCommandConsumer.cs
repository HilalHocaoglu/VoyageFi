using MassTransit;
using Microsoft.Extensions.Logging;
using VoyageFi.Core.Events;

namespace VoyageFi.Infrastructure.Consumers;

/// <summary>
/// Admin Dashboard'dan gelen AdminCommandEvent mesajlarını RabbitMQ'dan tüketen consumer.
///
/// FAZ 2 – Adım 1: Şimdilik her komutu ILogger ile konsola yazar (kanıt: event-driven döngü çalışıyor).
/// FAZ 2 – Adım 3: Buraya AuditLog PostgreSQL yazımı ve SignalR ReceiveAuditEntry push'u eklenecek.
/// </summary>
public class AdminCommandConsumer : IConsumer<AdminCommandEvent>
{
    private readonly ILogger<AdminCommandConsumer> _logger;

    public AdminCommandConsumer(ILogger<AdminCommandConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AdminCommandEvent> context)
    {
        var cmd = context.Message;

        _logger.LogInformation(
            "🎯 [AdminCommand] Komut alındı | ID: {CommandId} | Tip: {CommandType} | " +
            "Uçuş: {FlightNumber} | PNR: {PnrCode} | Rötar: {Delay} dk | " +
            "Aktör: {ActorId} | Zaman: {IssuedAt:u}",
            cmd.CommandId,
            cmd.CommandType,
            cmd.FlightNumber,
            cmd.PnrCode,
            cmd.DelayDurationMinutes,
            cmd.ActorId,
            cmd.IssuedAt);

        // ─── FAZ 2 Adım 1: Consumer çalışıyor, döngü kanıtlandı ───────────────────
        // Sonraki adımlarda buraya eklenecek:
        //   Adım 3 → await _auditLogRepository.WriteAsync(...)
        //   Adım 3 → await _hubContext.Clients.All.SendAsync("ReceiveAuditEntry", ...)
        //   Adım 2 → "AI_NOTIFY" tipinde → await _agentService.ResolveAsync(...)

        await Task.CompletedTask;
    }
}
