namespace VoyageFi.Core.Events;

public record FlightDelayedEvent(string PnrCode, string FlightNumber, string UserId, int DelayDurationMinutes);
