using pm.Application.DTOs.Clients;
using pm.Application.Interfaces;
using pm.Domain.Entities;

namespace pm.Application.Services;

public class ClientService : IClientService
{
    private readonly IClientRepository _clientRepository;

    public ClientService(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    public async Task<IReadOnlyList<ClientResponse>> GetAllAsync(Guid userId)
    {
        var clients = await _clientRepository.GetAllByUserIdAsync(userId);
        return clients.Select(MapToResponse).ToList();
    }

    public async Task<ClientResponse> GetByIdAsync(Guid userId, Guid id)
    {
        var client = await _clientRepository.GetByIdAsync(userId, id)
            ?? throw new KeyNotFoundException("Client not found.");

        return MapToResponse(client);
    }

    public async Task<ClientResponse> CreateAsync(Guid userId, CreateClientRequest request)
    {
        var now = DateTime.UtcNow;
        ValidateClientName(request.FirstName, request.LastName, request.CompanyName);

        var client = new Client
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClientType = NormalizeClientType(request.ClientType),
            FirstName = request.FirstName,
            LastName = request.LastName,
            CompanyName = request.CompanyName,
            CompanyCode = request.CompanyCode,
            VatCode = request.VatCode,
            Email = request.Email,
            Phone = request.Phone,
            BankIban = request.BankIban,
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            City = request.City,
            PostalCode = request.PostalCode,
            CountryCode = NormalizeCountryCode(request.CountryCode),
            Notes = request.Notes,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _clientRepository.CreateAsync(client);
        return MapToResponse(client);
    }

    public async Task<ClientResponse> UpdateAsync(Guid userId, Guid id, UpdateClientRequest request)
    {
        var client = await _clientRepository.GetByIdAsync(userId, id)
            ?? throw new KeyNotFoundException("Client not found.");

        ValidateClientName(request.FirstName, request.LastName, request.CompanyName);

        client.ClientType = NormalizeClientType(request.ClientType);
        client.FirstName = request.FirstName;
        client.LastName = request.LastName;
        client.CompanyName = request.CompanyName;
        client.CompanyCode = request.CompanyCode;
        client.VatCode = request.VatCode;
        client.Email = request.Email;
        client.Phone = request.Phone;
        client.BankIban = request.BankIban;
        client.AddressLine1 = request.AddressLine1;
        client.AddressLine2 = request.AddressLine2;
        client.City = request.City;
        client.PostalCode = request.PostalCode;
        client.CountryCode = NormalizeCountryCode(request.CountryCode);
        client.Notes = request.Notes;
        client.IsActive = request.IsActive;
        client.UpdatedAt = DateTime.UtcNow;

        await _clientRepository.UpdateAsync(client);
        return MapToResponse(client);
    }

    public async Task DeleteAsync(Guid userId, Guid id)
    {
        var client = await _clientRepository.GetByIdAsync(userId, id)
            ?? throw new KeyNotFoundException("Client not found.");

        if (client.DeletedAt.HasValue)
            return;

        await _clientRepository.SoftDeleteAsync(userId, id);
    }

    private static ClientResponse MapToResponse(Client client) =>
        new(
            client.Id,
            client.UserId,
            client.ClientType,
            client.FirstName,
            client.LastName,
            client.CompanyName,
            client.CompanyCode,
            client.VatCode,
            client.Email,
            client.Phone,
            client.BankIban,
            client.AddressLine1,
            client.AddressLine2,
            client.City,
            client.PostalCode,
            client.CountryCode,
            client.Notes,
            client.IsActive,
            client.CreatedAt,
            client.UpdatedAt);

    private static void ValidateClientName(string? firstName, string? lastName, string? companyName)
    {
        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(companyName))
            throw new InvalidOperationException("At least first name or company name must be provided.");
    }

    private static string NormalizeClientType(string? clientType)
    {
        var value = string.IsNullOrWhiteSpace(clientType) ? "individual" : clientType.Trim().ToLowerInvariant();
        return value is "individual" or "company"
            ? value
            : throw new InvalidOperationException("Invalid client type.");
    }

    private static string NormalizeCountryCode(string? countryCode) =>
        string.IsNullOrWhiteSpace(countryCode)
            ? "LT"
            : countryCode.Trim().ToUpperInvariant();
}


