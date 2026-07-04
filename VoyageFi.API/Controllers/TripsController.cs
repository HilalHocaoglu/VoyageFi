using Microsoft.AspNetCore.Mvc;
using VoyageFi.Application.DTOs;
using VoyageFi.Core.Entities;
using VoyageFi.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace VoyageFi.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")] // Dışarıdan "http://localhost:port/api/trips" adresiyle ulaşılacak
public class TripsController : ControllerBase
{
    private readonly ITripRepository _tripRepository;

    public TripsController(ITripRepository tripRepository)
    {
        _tripRepository = tripRepository;
    }

    // CREATE (Ekleme) İşlemi - POST isteği
    [HttpPost]
    public async Task<IActionResult> CreateTrip([FromBody] CreateTripDto request)
    {
        // 1. (DTO) ----> (Entity) çevir (Mapping)
        var trip = new Trip
        {
            PnrCode = request.PnrCode,
            Airline = request.Airline,
            DepartureAirport = request.DepartureAirport,
            ArrivalAirport = request.ArrivalAirport,
            DepartureTime = request.DepartureTime,
            UserId = request.UserId
        };

        // 2. Veritabanına kaydet
        var createdTrip = await _tripRepository.AddTripAsync(trip);

        // 3. (Response DTO) Sunum tabağına koy
        var response = new TripResponseDto
        {
            Id = createdTrip.Id,
            PnrCode = createdTrip.PnrCode,
            Airline = createdTrip.Airline,
            Route = $"{createdTrip.DepartureAirport} -> {createdTrip.ArrivalAirport}",
            DepartureTime = createdTrip.DepartureTime,
            Status = createdTrip.Status
        };

        // 4. Müşteriye servis et
        return Ok(response);
    }

    // READ (Listeleme) İşlemi - GET isteği
    [HttpGet]
    public async Task<IActionResult> GetAllTrips()
    {

    

        // 1. Mutfaktan tüm listeyi iste
        var trips = await _tripRepository.GetAllTripsAsync();
        
        // 2. Tenceredeki yemeği sunum tabaklarına (DTO listesine) bölüştür
        var responseList = trips.Select(t => new TripResponseDto
        {
            Id = t.Id,
            PnrCode = t.PnrCode,
            Airline = t.Airline,
            Route = $"{t.DepartureAirport} -> {t.ArrivalAirport}",
            DepartureTime = t.DepartureTime,
            Status = t.Status
        }).ToList();

        // 3. Müşteriye servis et
        return Ok(responseList);
    } // <-- DÜZELTME: Kapanış parantezi buraya alındı.

    // UPDATE (Güncelleme) İşlemi - PUT isteği
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTrip(Guid id, [FromBody] UpdateTripDto request)
    {
        // 1. Kilerde böyle bir uçuş var mı diye kontrol et
        var existingTrip = await _tripRepository.GetTripByIdAsync(id);
        if (existingTrip == null)
            return NotFound("Güncellenmek istenen uçuş bulunamadı."); // 404 Hatası dön

        // 2. Varsa, yeni gelen verileri eski kaydın üzerine yaz
        existingTrip.PnrCode = request.PnrCode;
        existingTrip.Airline = request.Airline;
        existingTrip.DepartureTime = request.DepartureTime;
        existingTrip.Status = request.Status;

        // 3. Aşçıya ver, veritabanına kaydetsin
        await _tripRepository.UpdateTripAsync(existingTrip);

        return Ok("Uçuş başarıyla güncellendi.");
    }

    // DELETE (Silme) İşlemi - DELETE isteği
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTrip(Guid id)
    {
        // 1. Uçuşu bul
        var existingTrip = await _tripRepository.GetTripByIdAsync(id);
        if (existingTrip == null)
            return NotFound("Silinmek istenen uçuş bulunamadı.");

        // 2. Sil
        await _tripRepository.DeleteTripAsync(id);

        return Ok("Uçuş başarıyla silindi.");
    }
}