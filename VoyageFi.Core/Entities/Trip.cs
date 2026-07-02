namespace VoyageFi.Core.Entities;

public class Trip
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PnrCode { get; set; } = string.Empty;
    public string Airline { get; set; } = string.Empty;
    public string DepartureAirport { get; set; } = string.Empty;
    public string ArrivalAirport { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public string Status { get; set; } = "OnTime"; // OnTime, Delayed, Cancelled

    
    public Guid UserId { get; set; }
    public User? User { get; set; }

    // Bir seyahatin birden fazla harcaması olabilir (One-to-Many)
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}