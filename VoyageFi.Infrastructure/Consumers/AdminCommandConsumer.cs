using System.Diagnostics;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using VoyageFi.Core.Entities;
using VoyageFi.Core.Events;
using VoyageFi.Core.Interfaces;
using VoyageFi.Infrastructure.Data;

namespace VoyageFi.Infrastructure.Consumers;

/// <summary>
/// Admin Dashboard'dan gelen AdminCommandEvent mesajlarını RabbitMQ'dan tüketen consumer.
///
/// FAZ 2 – Adım 1: Her komut yakalanır ve konsola loglanır.
/// FAZ 2 – Adım 3: Her komut PostgreSQL admin_audit_log tablosuna yazılır;
///                  ardından SignalR ReceiveAuditEntry event'i ile tüm istemcilere yayınlanır.
/// </summary>
public class AdminCommandConsumer : IConsumer<AdminCommandEvent>
{
    private readonly ILogger<AdminCommandConsumer> _logger;
    private readonly VoyageFiDbContext             _db;
    private readonly IFlightNotificationService    _notificationService;

    public AdminCommandConsumer(
        ILogger<AdminCommandConsumer> logger,
        VoyageFiDbContext db,
        IFlightNotificationService notificationService)
    {
        _logger = logger;
        _db     = db;
        _notificationService = notificationService;
    }

    public async Task Consume(ConsumeContext<AdminCommandEvent> context)
    {
        var cmd = context.Message;
        var sw  = Stopwatch.StartNew();

        _logger.LogInformation(
            "🎯 [AdminCommand] Komut alındı | ID: {CommandId} | Tip: {CommandType} | " +
            "Uçuş: {FlightNumber} | PNR: {PnrCode} | Rötar: {Delay} dk | " +
            "Aktör: {ActorId} | Zaman: {IssuedAt:u}",
            cmd.CommandId, cmd.CommandType, cmd.FlightNumber,
            cmd.PnrCode, cmd.DelayDurationMinutes, cmd.ActorId, cmd.IssuedAt);

        // ─── Adım 3: Audit Log → PostgreSQL ──────────────────────────────────
        var payload = JsonSerializer.Serialize(new
        {
            cmd.FlightNumber,
            cmd.PnrCode,
            cmd.DelayDurationMinutes,
            cmd.CommandId,
        });

        sw.Stop();

        var auditEntry = new AdminAuditLog
        {
            Id         = Guid.NewGuid(),
            Actor      = cmd.ActorId,
            Command    = cmd.CommandType,
            Payload    = payload,
            Result     = "SUCCESS",
            DurationMs = (int)sw.ElapsedMilliseconds,
            CreatedAt  = DateTime.UtcNow,
        };

        _db.AdminAuditLogs.Add(auditEntry);
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "📝 [AuditTrail] Kayıt yazıldı | ID: {AuditId} | {DurationMs}ms",
            auditEntry.Id, auditEntry.DurationMs);

        await _notificationService.BroadcastAuditEntryAsync(
            auditEntry.Id,
            auditEntry.Actor,
            auditEntry.Command,
            cmd.FlightNumber,
            auditEntry.Result,
            auditEntry.DurationMs,
            auditEntry.CreatedAt,
            context.CancellationToken);

        _logger.LogInformation(
            "📡 [SignalR] ReceiveAuditEntry yayınlandı | Komut: {CommandType} | Uçuş: {FlightNumber}",
            cmd.CommandType, cmd.FlightNumber);
    }
}
