namespace pm.Application.DTOs.Projects;

public record UpdateProjectRequest(
    Guid ClientId,
    string Name,
    string? Description,
    string? AgreedScope,
    string? PricingType,
    decimal? AgreedAmount,
    string? Currency,
    decimal? VatRate,
    int? PaymentTermsDays,
    DateOnly? StartsOn,
    DateOnly? DueOn
);
