namespace pm.Application.DTOs.Projects;

public record CreateProjectRequest(
    Guid ClientId,
    string Name,
    string? Description,
    string? AgreedScope,
    string? Status,
    string? PricingType,
    decimal? AgreedAmount,
    string? Currency,
    decimal? VatRate,
    int? PaymentTermsDays,
    DateOnly? StartsOn,
    DateOnly? DueOn
);
