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
        var client = new Client
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClientType = NormalizeClientType(request.ClientType),
            Name = request.Name,
            LegalName = request.LegalName,
            Email = request.Email,
            Phone = request.Phone,
            CompanyCode = request.CompanyCode,
            VatCode = request.VatCode,
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

        client.ClientType = NormalizeClientType(request.ClientType);
        client.Name = request.Name;
        client.LegalName = request.LegalName;
        client.Email = request.Email;
        client.Phone = request.Phone;
        client.CompanyCode = request.CompanyCode;
        client.VatCode = request.VatCode;
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
            client.Name,
            client.LegalName,
            client.Email,
            client.Phone,
            client.CompanyCode,
            client.VatCode,
            client.AddressLine1,
            client.AddressLine2,
            client.City,
            client.PostalCode,
            client.CountryCode,
            client.Notes,
            client.IsActive,
            client.CreatedAt,
            client.UpdatedAt);

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


