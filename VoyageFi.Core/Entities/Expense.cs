namespace VoyageFi.Core.Entities;

public class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string Category { get; set; } = string.Empty; // Yemek, Ulaşım vb.
    public string Description { get; set; } = string.Empty; 
    public string? ReceiptImageUrl { get; set; } 
    public DateTime Date { get; set; }

    // İlişkiler
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }
}