using pm.Domain.Entities;

namespace pm.Application.Interfaces;

public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> GetAllByUserIdAsync(Guid userId);
    Task<Project?> GetByIdAsync(Guid userId, Guid id);
    Task<Project> CreateAsync(Project project);
    Task UpdateAsync(Project project);
    Task SoftDeleteAsync(Guid userId, Guid id);
    Task AddStatusHistoryAsync(Guid projectId, Guid changedByUserId, string? fromStatus, string toStatus);
}
