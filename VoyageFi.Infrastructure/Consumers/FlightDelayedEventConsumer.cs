using MassTransit;
using Microsoft.Extensions.Logging;
using VoyageFi.Core.Events;

namespace VoyageFi.Infrastructure.Consumers;

public class FlightDelayedEventConsumer : IConsumer<FlightDelayedEvent>
{
    private readonly ILogger<FlightDelayedEventConsumer> _logger;

    public FlightDelayedEventConsumer(ILogger<FlightDelayedEventConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<FlightDelayedEvent> context)
    {
        var message = context.Message;
        
        _logger.LogInformation(
            "✈️ [RabbitMQ] Mesaj alındı: {FlightNumber} (PNR: {PnrCode}) uçuşu {Delay} dakika rötar yaptı. AI asistanı (Kullanıcı: {UserId}) bilgilendirme için tetiklenebilir.",
            message.FlightNumber, 
            message.PnrCode, 
            message.DelayDurationMinutes,
            message.UserId);

        return Task.CompletedTask;
    }
}
