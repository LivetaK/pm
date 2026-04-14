using pm.Domain.Entities;

namespace pm.Application.Interfaces;

public interface IClientRepository
{
    Task<IReadOnlyList<Client>> GetAllByUserIdAsync(Guid userId);
    Task<Client?> GetByIdAsync(Guid userId, Guid id);
    Task<Client> CreateAsync(Client client);
    Task UpdateAsync(Client client);
    Task SoftDeleteAsync(Guid userId, Guid id);
}

