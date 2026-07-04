namespace VoyageFi.Application.DTOs;

public class CreateTripDto
{
    public string PnrCode { get; set; } = string.Empty;
    public string Airline { get; set; } = string.Empty;
    public string DepartureAirport { get; set; } = string.Empty;
    public string ArrivalAirport { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    
    // Hangi kullanıcının seyahati olduğunu bilmek için geçici olarak UserId'yi alıyoruz
    public Guid UserId { get; set; } 
}