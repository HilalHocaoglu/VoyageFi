namespace VoyageFi.Core.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }

    // Bir kullanıcının birden fazla seyahati olabilir (One-to-Many)
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}