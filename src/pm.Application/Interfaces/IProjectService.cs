using pm.Application.DTOs.Projects;

namespace pm.Application.Interfaces;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectResponse>> GetAllAsync(Guid userId);
    Task<ProjectResponse> GetByIdAsync(Guid userId, Guid id);
    Task<ProjectResponse> CreateAsync(Guid userId, CreateProjectRequest request);
    Task<ProjectResponse> UpdateAsync(Guid userId, Guid id, UpdateProjectRequest request);
    Task<ProjectResponse> UpdateStatusAsync(Guid userId, Guid id, UpdateProjectStatusRequest request);
    Task DeleteAsync(Guid userId, Guid id);
}
