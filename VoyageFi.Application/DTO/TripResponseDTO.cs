namespace VoyageFi.Application.DTOs;

public class TripResponseDto
{
    public Guid Id { get; set; }
    public string PnrCode { get; set; } = string.Empty;
    public string Airline { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty; // "IST -> JFK" gibi formatlayıp sunacağız
    public DateTime DepartureTime { get; set; }
    public string Status { get; set; } = string.Empty;
}