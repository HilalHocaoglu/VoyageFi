namespace VoyageFi.Application.DTOs;

public class UpdateTripDto
{
    public string PnrCode { get; set; } = string.Empty;
    public string Airline { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public string Status { get; set; } = string.Empty; // Örn: "Delayed", "Cancelled"
}