using Microsoft.EntityFrameworkCore;
using VoyageFi.Core.Entities;
using VoyageFi.Core.Interfaces;
using VoyageFi.Infrastructure.Data;

namespace VoyageFi.Infrastructure.Repositories;

// "TripRepository, ITripRepository sözleşmesini uygular" diyoruz
public class TripRepository : ITripRepository
{
    // Veritabanı tercümanımızı (DbContext) buraya çağırıyoruz
    private readonly VoyageFiDbContext _context;

    public TripRepository(VoyageFiDbContext context)
    {
        _context = context;
    }

    // Görev 1: Veritabanındaki "Trips" tablosuna git ve hepsini liste olarak getir
    public async Task<IEnumerable<Trip>> GetAllTripsAsync()
    {
        return await _context.Trips.ToListAsync();
    }

    // Görev 2: Dışarıdan gelen yeni seyahati veritabanına ekle ve kaydet
    public async Task<Trip> AddTripAsync(Trip trip)
    {
        await _context.Trips.AddAsync(trip);
        await _context.SaveChangesAsync(); // Değişiklikleri PostgreSQL'e fiziksel olarak yazar
        return trip;
    }

    // 1. Veritabanında belirtilen ID'ye sahip uçuşu bul getir
    public async Task<Trip?> GetTripByIdAsync(Guid id)
    {
        return await _context.Trips.FindAsync(id);
    }

    // 2. Bulunan uçuşun değişen verilerini PostgreSQL'e kaydet
    public async Task UpdateTripAsync(Trip trip)
    {
        _context.Trips.Update(trip);
        await _context.SaveChangesAsync();
    }

    // 3. Uçuşu bul ve veritabanından tamamen sil
    public async Task DeleteTripAsync(Guid id)
    {
        var trip = await _context.Trips.FindAsync(id);
        if (trip != null)
        {
            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
        }
    }
}