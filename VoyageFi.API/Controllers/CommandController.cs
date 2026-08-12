using MassTransit;
using Microsoft.AspNetCore.Mvc;
using VoyageFi.Core.Events;

namespace VoyageFi.API.Controllers;

/// <summary>
/// Admin Dashboard'dan gelen aksiyonları (komutları) alır ve RabbitMQ'ya iletir.
///
/// FAZ 2 – Adım 1: Bidirectional Command Bus'ın write-side endpoint'i.
/// İstek geldiği anda IBus.Publish() ile AdminCommandEvent oluşturulup kuyruktan atılır;
/// Adım 1 için [Authorize] kaldırıldı — Adım 2+ ile JWT koruması geri eklenir.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CommandController : ControllerBase
{
    private readonly IBus _bus;
    private readonly ILogger<CommandController> _logger;

    public CommandController(IBus bus, ILogger<CommandController> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Request body: Admin Dashboard'dan gelen komut.</summary>
    public sealed class DispatchRequest
    {
        /// <summary>Komut tipi: "SMS_NOTIFY" | "LOUNGE_GRANT" | "AI_NOTIFY"</summary>
        public string CommandType { get; set; } = string.Empty;

        public string FlightNumber        { get; set; } = string.Empty;
        public string PnrCode             { get; set; } = string.Empty;
        public int    DelayDurationMinutes { get; set; }

        /// <summary>
        /// Komutu başlatan admin. Şimdilik frontend'den geliyor;
        /// Adım 2'de JWT claim'den okunacak.
        /// </summary>
        public string ActorId { get; set; } = "admin@voyagefi.com";
    }

    /// <summary>
    /// POST /api/command/dispatch
    /// Gelen komutu doğrular, AdminCommandEvent'e dönüştürür ve RabbitMQ'ya publish eder.
    /// </summary>
    [HttpPost("dispatch")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DispatchAsync(
        [FromBody] DispatchRequest request,
        CancellationToken cancellationToken)
    {
        // Temel doğrulama
        var validCommandTypes = new[] { "SMS_NOTIFY", "LOUNGE_GRANT", "AI_NOTIFY" };
        if (!validCommandTypes.Contains(request.CommandType))
        {
            return BadRequest(new { Error = $"Geçersiz komut tipi: '{request.CommandType}'. " +
                                            $"Geçerli tipler: {string.Join(", ", validCommandTypes)}" });
        }

        if (string.IsNullOrWhiteSpace(request.FlightNumber))
            return BadRequest(new { Error = "FlightNumber boş olamaz." });

        // Domain event oluştur
        var commandId = Guid.NewGuid();
        var @event = new AdminCommandEvent(
            CommandId:            commandId,
            ActorId:              request.ActorId,
            CommandType:          request.CommandType,
            FlightNumber:         request.FlightNumber,
            PnrCode:              request.PnrCode,
            DelayDurationMinutes: request.DelayDurationMinutes,
            IssuedAt:             DateTime.UtcNow
        );

        // RabbitMQ'ya gönder (fire-and-forget — 202 hemen döner)
        await _bus.Publish(@event, cancellationToken);

        _logger.LogInformation(
            "📤 [CommandBus] Komut kuyruğa alındı | ID: {CommandId} | Tip: {CommandType} | Uçuş: {FlightNumber}",
            commandId, request.CommandType, request.FlightNumber);

        return Accepted(new
        {
            CommandId   = commandId,
            Status      = "QUEUED",
            Message     = $"{request.CommandType} komutu RabbitMQ kuyruğuna alındı.",
            FlightNumber = request.FlightNumber
        });
    }
}
