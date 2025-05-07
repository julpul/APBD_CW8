using Microsoft.AspNetCore.Mvc;
using Tutorial8.Services;

namespace Tutorial8.Controllers
{
    [Route("api")]
    [ApiController]
    public class TripsController : ControllerBase
    {
        private readonly ITripsService _tripsService;

        public TripsController(ITripsService tripsService)
        {
            _tripsService = tripsService;
        }

        /// <summary>
        /// Zwraca listę wszystkich wycieczek wraz z przypisanymi krajami.
        /// </summary>
        [HttpGet("trips")]
        public async Task<IActionResult> GetTrips()
        {
            var trips = await _tripsService.GetTrips();
            return Ok(trips);
        }

        /// <summary>
        /// Zwraca wszystkie wycieczki, na które zapisany jest klient.
        /// </summary>
        [HttpGet("clients/{clientId}/trips")]
        public async Task<IActionResult> GetClientTrips(int clientId)
        {
            if (clientId <= 0)
                return BadRequest("Client ID must be a positive number.");

            var trips = await _tripsService.GetClientTrips(clientId);

            if (trips == null)
                return NotFound($"Client with ID {clientId} does not exist.");

            return Ok(trips);
        }

        /// <summary>
        /// Tworzy nowego klienta na podstawie danych w ciele żądania.
        /// </summary>
        [HttpPost("clients")]
        public async Task<IActionResult> CreateClient([FromBody] CreateClientDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest("Nieprawidłowe dane.");

            var newId = await _tripsService.CreateClientAsync(dto);

            if (newId == null)
                return BadRequest("Nie udało się dodać klienta. Sprawdź dane.");

            return Created($"/api/clients/{newId}", new { Id = newId });
        }

        /// <summary>
        /// Rejestruje klienta na wycieczkę, jeśli są miejsca.
        /// </summary>
        [HttpPut("clients/{clientId}/trips/{tripId}")]
        public async Task<IActionResult> RegisterClientToTrip(int clientId, int tripId)
        {
            if (clientId <= 0 || tripId <= 0)
                return BadRequest("ClientId and TripId must be positive.");

            var result = await _tripsService.RegisterClientToTripAsync(clientId, tripId);

            if (result == "Client does not exist")
                return NotFound("Client not found.");
            if (result == "Trip does not exist")
                return NotFound("Trip not found.");
            if (result == "Trip has reached the maximum number of participants")
                return Conflict("Trip is full.");
            if (result == "Client is already registered to this trip")
                return Conflict("Client already registered.");

            return Ok("Successfully registered");
        }

        /// <summary>
        /// Usuwa rejestrację klienta z danej wycieczki.
        /// </summary>
        [HttpDelete("clients/{clientId}/trips/{tripId}")]
        public async Task<IActionResult> UnregisterClientFromTrip(int clientId, int tripId)
        {
            if (clientId <= 0 || tripId <= 0)
                return BadRequest("IDs must be positive integers.");

            var result = await _tripsService.UnregisterClientFromTripAsync(clientId, tripId);

            if (result == "Registration does not exist")
                return NotFound("Client is not registered for this trip.");

            return Ok("Client was successfully unregistered from the trip.");
        }
    }
}

