namespace pm.Application.DTOs.Projects;

public record ProjectResponse(
    Guid Id,
    Guid UserId,
    Guid ClientId,
    string Name,
    string? Description,
    string? AgreedScope,
    string Status,
    string PricingType,
    decimal? AgreedAmount,
    string Currency,
    decimal VatRate,
    int PaymentTermsDays,
    DateOnly? StartsOn,
    DateOnly? DueOn,
    DateTime? WorkCompletedAt,
    DateTime? InvoicedAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
