using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Tutorial8.Models.DTOs;

namespace Tutorial8.Services;

public class TripsService : ITripsService
{
    private readonly string _connectionString =
        "Server=localhost\\SQLEXPRESS01;Database=apbd8;Trusted_Connection=True;TrustServerCertificate=True;";


public async Task<List<TripDTO>> GetTrips()
{
    var trips = new List<TripDTO>();
    
    // Zwraca listę wycieczek i ich krajów
    string command = @"
        SELECT 
            t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
            c.Name AS CountryName
        FROM Trip t
        LEFT JOIN Country_Trip ct ON t.IdTrip = ct.IdTrip
        LEFT JOIN Country c ON ct.IdCountry = c.IdCountry";

    try
    {
        using (var conn = new SqlConnection(_connectionString))
        using (var cmd = new SqlCommand(command, conn))
        {
            await conn.OpenAsync();

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                var tripDict = new Dictionary<int, TripDTO>();

                while (await reader.ReadAsync())
                {
                    int idTrip = reader.GetInt32(reader.GetOrdinal("IdTrip"));

                    if (!tripDict.ContainsKey(idTrip))
                    {
                        if (reader.IsDBNull(reader.GetOrdinal("Name")) ||
                            reader.IsDBNull(reader.GetOrdinal("Description")) ||
                            reader.IsDBNull(reader.GetOrdinal("DateFrom")) ||
                            reader.IsDBNull(reader.GetOrdinal("DateTo")) ||
                            reader.IsDBNull(reader.GetOrdinal("MaxPeople")))
                        {
                            continue;
                        }

                        tripDict[idTrip] = new TripDTO
                        {
                            Id = idTrip,
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            Description = reader.GetString(reader.GetOrdinal("Description")),
                            DateFrom = reader.GetDateTime(reader.GetOrdinal("DateFrom")),
                            DateTo = reader.GetDateTime(reader.GetOrdinal("DateTo")),
                            MaxPeople = reader.GetInt32(reader.GetOrdinal("MaxPeople")),
                            Countries = new List<CountryDTO>()
                        };
                    }

                    if (!reader.IsDBNull(reader.GetOrdinal("CountryName")))
                    {
                        tripDict[idTrip].Countries.Add(new CountryDTO
                        {
                            Name = reader.GetString(reader.GetOrdinal("CountryName"))
                        });
                    }
                }

                trips = tripDict.Values.ToList();
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Błąd: {ex.Message}");
        
    }

    return trips;
}
public async Task<List<ClientTripDTO>?> GetClientTrips(int clientId)
{
    var trips = new List<ClientTripDTO>();
    
    // Sprawdzenie, czy klient istnieje
    string checkClientQuery = "SELECT COUNT(1) FROM Client WHERE IdClient = @IdClient";

    using (var conn = new SqlConnection(_connectionString))
    using (var checkCmd = new SqlCommand(checkClientQuery, conn))
    {
        checkCmd.Parameters.AddWithValue("@IdClient", clientId);
        await conn.OpenAsync();

        var count = (int)await checkCmd.ExecuteScalarAsync();
        if (count == 0)
            return null;

        conn.Close();
    }
    
    // Pobiera wycieczki przypisane do klienta wraz z danymi o rejestracji
    string tripsQuery = @"
        SELECT 
            t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
            ct.RegisteredAt, ct.PaymentDate
        FROM Trip t
        INNER JOIN Client_Trip ct ON t.IdTrip = ct.IdTrip
        WHERE ct.IdClient = @IdClient";

    using (var conn = new SqlConnection(_connectionString))
    using (var cmd = new SqlCommand(tripsQuery, conn))
    {
        cmd.Parameters.AddWithValue("@IdClient", clientId);
        await conn.OpenAsync();

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                // Konwersja RegisteredAt z int na DateTime
                int registeredAtInt = reader.GetInt32(reader.GetOrdinal("RegisteredAt"));
                int? paymentDateInt = reader.IsDBNull(reader.GetOrdinal("PaymentDate"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("PaymentDate"));

                trips.Add(new ClientTripDTO
                {
                    Id = reader.GetInt32(reader.GetOrdinal("IdTrip")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    Description = reader.GetString(reader.GetOrdinal("Description")),
                    DateFrom = reader.GetDateTime(reader.GetOrdinal("DateFrom")),
                    DateTo = reader.GetDateTime(reader.GetOrdinal("DateTo")),
                    MaxPeople = reader.GetInt32(reader.GetOrdinal("MaxPeople")),
                    RegisteredAt = DateTimeOffset.FromUnixTimeSeconds(registeredAtInt).DateTime,
                    PaymentDate = paymentDateInt.HasValue
                        ? DateTimeOffset.FromUnixTimeSeconds(paymentDateInt.Value).DateTime
                        : null
                });
            }
        }
    }

    return trips;
}
public async Task<int?> CreateClientAsync(CreateClientDTO clientDto)
{
    if (string.IsNullOrWhiteSpace(clientDto.FirstName) ||
        string.IsNullOrWhiteSpace(clientDto.LastName) ||
        string.IsNullOrWhiteSpace(clientDto.Email) ||
        string.IsNullOrWhiteSpace(clientDto.Telephone) ||
        string.IsNullOrWhiteSpace(clientDto.Pesel))
    {
        return null;
    }
    // INSERT nowego klienta i zwrócenie jego ID
    const string insertQuery = @"
        INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
        OUTPUT INSERTED.IdClient
        VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel)";

    using (var conn = new SqlConnection(_connectionString))
    using (var cmd = new SqlCommand(insertQuery, conn))
    {
        cmd.Parameters.AddWithValue("@FirstName", clientDto.FirstName);
        cmd.Parameters.AddWithValue("@LastName", clientDto.LastName);
        cmd.Parameters.AddWithValue("@Email", clientDto.Email);
        cmd.Parameters.AddWithValue("@Telephone", clientDto.Telephone);
        cmd.Parameters.AddWithValue("@Pesel", clientDto.Pesel);

        await conn.OpenAsync();
        var insertedId = await cmd.ExecuteScalarAsync();

        return insertedId != null ? Convert.ToInt32(insertedId) : null;
    }
}
public async Task<string?> RegisterClientToTripAsync(int clientId, int tripId)
{
    using var conn = new SqlConnection(_connectionString);
    await conn.OpenAsync();
    //sprawdzanie czy klient istnieje
    var checkClient = new SqlCommand("SELECT COUNT(1) FROM Client WHERE IdClient = @ClientId", conn);
    checkClient.Parameters.AddWithValue("@ClientId", clientId);
    var clientExists = (int)await checkClient.ExecuteScalarAsync() > 0;
    if (!clientExists)
        return "Client does not exist";
    // Sprawdzenie liczby zapisanych klientów
    var checkTrip = new SqlCommand("SELECT MaxPeople FROM Trip WHERE IdTrip = @TripId", conn);
    checkTrip.Parameters.AddWithValue("@TripId", tripId);
    var maxPeopleObj = await checkTrip.ExecuteScalarAsync();
    if (maxPeopleObj == null)
        return "Trip does not exist";

    int maxPeople = (int)maxPeopleObj;
    //pobnranie wszystkich osob bioracych udzial w wycieczce
    var countQuery = new SqlCommand(
        "SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @TripId", conn);
    countQuery.Parameters.AddWithValue("@TripId", tripId);
    int currentCount = (int)await countQuery.ExecuteScalarAsync();

    if (currentCount >= maxPeople)
        return "Trip has reached the maximum number of participants";
    
    var alreadyRegistered = new SqlCommand(@"
        SELECT COUNT(*) FROM Client_Trip 
        WHERE IdClient = @ClientId AND IdTrip = @TripId", conn);
    alreadyRegistered.Parameters.AddWithValue("@ClientId", clientId);
    alreadyRegistered.Parameters.AddWithValue("@TripId", tripId);

    if ((int)await alreadyRegistered.ExecuteScalarAsync() > 0)
        return "Client is already registered to this trip";
    
    var insert = new SqlCommand(@"
        INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt)
        VALUES (@ClientId, @TripId, @RegisteredAt)", conn);
    insert.Parameters.AddWithValue("@ClientId", clientId);
    insert.Parameters.AddWithValue("@TripId", tripId);
    insert.Parameters.AddWithValue("@RegisteredAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    await insert.ExecuteNonQueryAsync();
    return null;
}

public async Task<string?> UnregisterClientFromTripAsync(int clientId, int tripId)
{
    using var conn = new SqlConnection(_connectionString);
    await conn.OpenAsync();
    
    var checkCmd = new SqlCommand(@"
        SELECT COUNT(*) FROM Client_Trip 
        WHERE IdClient = @ClientId AND IdTrip = @TripId", conn);

    checkCmd.Parameters.AddWithValue("@ClientId", clientId);
    checkCmd.Parameters.AddWithValue("@TripId", tripId);

    var exists = (int)await checkCmd.ExecuteScalarAsync() > 0;
    if (!exists)
        return "Registration does not exist";
    // DELETE rejestracji klienta z wycieczki
    var deleteCmd = new SqlCommand(@"
        DELETE FROM Client_Trip 
        WHERE IdClient = @ClientId AND IdTrip = @TripId", conn);

    deleteCmd.Parameters.AddWithValue("@ClientId", clientId);
    deleteCmd.Parameters.AddWithValue("@TripId", tripId);

    await deleteCmd.ExecuteNonQueryAsync();

    return null;
}

}