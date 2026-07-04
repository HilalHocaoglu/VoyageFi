using VoyageFi.Core.Entities;

namespace VoyageFi.Core.Interfaces;

public interface ITripRepository
{
    Task<IEnumerable<Trip>> GetAllTripsAsync();
    Task<Trip> AddTripAsync(Trip trip);
    
    
    Task<Trip?> GetTripByIdAsync(Guid id); 
    Task UpdateTripAsync(Trip trip);       
    Task DeleteTripAsync(Guid id);      
}