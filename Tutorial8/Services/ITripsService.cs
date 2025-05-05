using Tutorial8.Models.DTOs;

namespace Tutorial8.Services;

public interface ITripsService
{
    Task<List<TripDTO>> GetTrips();

    Task<List<ClientTripDTO>?> GetClientTrips(int clientId);
    Task<int?> CreateClientAsync(CreateClientDTO clientDto);

    Task<string?> RegisterClientToTripAsync(int clientId, int tripId);
    
    Task<string?> UnregisterClientFromTripAsync(int clientId, int tripId);

    
}