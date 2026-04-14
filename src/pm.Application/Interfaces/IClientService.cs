using pm.Application.DTOs.Clients;

namespace pm.Application.Interfaces;

public interface IClientService
{
    Task<IReadOnlyList<ClientResponse>> GetAllAsync(Guid userId);
    Task<ClientResponse> GetByIdAsync(Guid userId, Guid id);
    Task<ClientResponse> CreateAsync(Guid userId, CreateClientRequest request);
    Task<ClientResponse> UpdateAsync(Guid userId, Guid id, UpdateClientRequest request);
    Task DeleteAsync(Guid userId, Guid id);
}

