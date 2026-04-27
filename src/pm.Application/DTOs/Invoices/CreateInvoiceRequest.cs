namespace pm.Application.DTOs.Invoices;

public record CreateInvoiceRequest(
    Guid ClientId,
    Guid? ProjectId,
    string? LanguageCode,
    DateOnly? IssueDate,
    DateOnly DueDate,
    string? Currency,
    string? Notes,
    List<LineItemRequest> LineItems
);
