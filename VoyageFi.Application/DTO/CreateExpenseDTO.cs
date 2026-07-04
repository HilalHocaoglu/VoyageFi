namespace VoyageFi.Application.DTOs;

public class CreateExpenseDto
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty; // "TRY", "USD", "EUR"
    public string Category { get; set; } = string.Empty; // "Food", "Transport", "Accommodation"
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    
    // Bu harcama hangi seyahate ait?
    public Guid TripId { get; set; } 
}