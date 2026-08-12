using MassTransit;
using Microsoft.Extensions.Logging;
using VoyageFi.Core.Events;
using VoyageFi.Core.Interfaces;

namespace VoyageFi.Infrastructure.Consumers;

public class FlightDelayedEventConsumer : IConsumer<FlightDelayedEvent>
{
    private readonly ILogger<FlightDelayedEventConsumer> _logger;
    private readonly IFlightNotificationService _notificationService;

    public FlightDelayedEventConsumer(
        ILogger<FlightDelayedEventConsumer> logger,
        IFlightNotificationService notificationService)
    {
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task Consume(ConsumeContext<FlightDelayedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "✈️ [RabbitMQ] Mesaj alındı: {FlightNumber} (PNR: {PnrCode}) uçuşu {Delay} dakika rötar yaptı. AI asistanı (Kullanıcı: {UserId}) bilgilendirme için tetiklenebilir.",
            message.FlightNumber,
            message.PnrCode,
            message.DelayDurationMinutes,
            message.UserId);

        // Admin Dashboard'a gerçek zamanlı rötar bildirimi gönder
        await _notificationService.BroadcastFlightDelayAsync(
            pnrCode: message.PnrCode,
            flightNumber: message.FlightNumber,
            userId: message.UserId,
            delayDurationMinutes: message.DelayDurationMinutes,
            cancellationToken: context.CancellationToken);

        // ─── FAZ 2 - Adım 4: Kural Tabanlı Kriz Algılama (Rule Engine) ───
        if (message.DelayDurationMinutes >= 180)
        {
            _logger.LogWarning("⚠️ [RuleEngine] Kriz algılandı! {FlightNumber} uçuşu {Delay} dk rötar yaptı.", message.FlightNumber, message.DelayDurationMinutes);

            await _notificationService.BroadcastIncidentAlertAsync(
                flightNumber: message.FlightNumber,
                reason: $"Kritik Rötar: {message.DelayDurationMinutes} dk",
                delayDurationMinutes: message.DelayDurationMinutes,
                cancellationToken: context.CancellationToken);
        }
    }
}
