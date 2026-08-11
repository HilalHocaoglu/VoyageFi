using Microsoft.AspNetCore.SignalR;

namespace VoyageFi.API.Hubs;

/// <summary>
/// Admin Dashboard ve diğer istemciler için gerçek zamanlı uçuş izleme Hub'ı.
/// İstemciler bu Hub'a bağlanarak anlık rötar bildirimlerini dinleyebilir.
/// </summary>
public class FlightMonitorHub : Hub
{
    private readonly ILogger<FlightMonitorHub> _logger;

    public FlightMonitorHub(ILogger<FlightMonitorHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("🔌 [SignalR] Yeni istemci bağlandı: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("🔌 [SignalR] İstemci bağlantısı kesildi: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
