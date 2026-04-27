using pm.Application.DTOs.Projects;
using pm.Application.Interfaces;
using pm.Domain.Entities;

namespace pm.Application.Services;

public class ProjectService : IProjectService
{
    private static readonly HashSet<string> ValidStatuses = new()
    {
        "draft", "active", "completed", "invoiced", "paid", "cancelled"
    };

    private static readonly HashSet<string> ValidPricingTypes = new()
    {
        "fixed", "hourly"
    };

    private readonly IProjectRepository _projectRepository;

    public ProjectService(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<IReadOnlyList<ProjectResponse>> GetAllAsync(Guid userId)
    {
        var projects = await _projectRepository.GetAllByUserIdAsync(userId);
        return projects.Select(MapToResponse).ToList();
    }

    public async Task<ProjectResponse> GetByIdAsync(Guid userId, Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(userId, id)
            ?? throw new KeyNotFoundException("Project not found.");
        return MapToResponse(project);
    }

    public async Task<ProjectResponse> CreateAsync(Guid userId, CreateProjectRequest request)
    {
        var status = NormalizeStatus(request.Status ?? "draft");
        var pricingType = NormalizePricingType(request.PricingType ?? "fixed");

        var now = DateTime.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClientId = request.ClientId,
            Name = request.Name,
            Description = request.Description,
            AgreedScope = request.AgreedScope,
            Status = status,
            PricingType = pricingType,
            AgreedAmount = request.AgreedAmount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "EUR" : request.Currency.ToUpperInvariant(),
            VatRate = request.VatRate ?? 21.00m,
            PaymentTermsDays = request.PaymentTermsDays ?? 14,
            StartsOn = request.StartsOn,
            DueOn = request.DueOn,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _projectRepository.CreateAsync(project);
        await _projectRepository.AddStatusHistoryAsync(project.Id, userId, null, project.Status);
        return MapToResponse(project);
    }

    public async Task<ProjectResponse> UpdateAsync(Guid userId, Guid id, UpdateProjectRequest request)
    {
        var project = await _projectRepository.GetByIdAsync(userId, id)
            ?? throw new KeyNotFoundException("Project not found.");

        project.ClientId = request.ClientId;
        project.Name = request.Name;
        project.Description = request.Description;
        project.AgreedScope = request.AgreedScope;
        project.PricingType = NormalizePricingType(request.PricingType ?? project.PricingType);
        project.AgreedAmount = request.AgreedAmount;
        project.Currency = string.IsNullOrWhiteSpace(request.Currency) ? project.Currency : request.Currency.ToUpperInvariant();
        project.VatRate = request.VatRate ?? project.VatRate;
        project.PaymentTermsDays = request.PaymentTermsDays ?? project.PaymentTermsDays;
        project.StartsOn = request.StartsOn;
        project.DueOn = request.DueOn;
        project.UpdatedAt = DateTime.UtcNow;

        await _projectRepository.UpdateAsync(project);
        return MapToResponse(project);
    }

    public async Task<ProjectResponse> UpdateStatusAsync(Guid userId, Guid id, UpdateProjectStatusRequest request)
    {
        var project = await _projectRepository.GetByIdAsync(userId, id)
            ?? throw new KeyNotFoundException("Project not found.");

        var newStatus = NormalizeStatus(request.Status);
        var oldStatus = project.Status;
        var now = DateTime.UtcNow;

        project.Status = newStatus;
        project.UpdatedAt = now;

        switch (newStatus)
        {
            case "completed":
                project.WorkCompletedAt ??= now;
                break;
            case "invoiced":
                project.InvoicedAt ??= now;
                break;
            case "paid":
                project.CompletedAt ??= now;
                break;
            case "cancelled":
                project.CancelledAt ??= now;
                break;
        }

        await _projectRepository.UpdateAsync(project);
        await _projectRepository.AddStatusHistoryAsync(project.Id, userId, oldStatus, newStatus);
        return MapToResponse(project);
    }

    public async Task DeleteAsync(Guid userId, Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(userId, id)
            ?? throw new KeyNotFoundException("Project not found.");

        if (project.DeletedAt.HasValue)
            return;

        await _projectRepository.SoftDeleteAsync(userId, id);
    }

    private static ProjectResponse MapToResponse(Project p) =>
        new(p.Id, p.UserId, p.ClientId, p.Name, p.Description, p.AgreedScope,
            p.Status, p.PricingType, p.AgreedAmount, p.Currency, p.VatRate,
            p.PaymentTermsDays, p.StartsOn, p.DueOn,
            p.WorkCompletedAt, p.InvoicedAt, p.CompletedAt, p.CancelledAt,
            p.CreatedAt, p.UpdatedAt);

    private static string NormalizeStatus(string status)
    {
        var v = status.Trim().ToLowerInvariant();
        return ValidStatuses.Contains(v) ? v
            : throw new InvalidOperationException($"Invalid project status '{status}'.");
    }

    private static string NormalizePricingType(string pricingType)
    {
        var v = pricingType.Trim().ToLowerInvariant();
        return ValidPricingTypes.Contains(v) ? v
            : throw new InvalidOperationException($"Invalid pricing type '{pricingType}'.");
    }
}
