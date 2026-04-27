namespace pm.Application.DTOs.Invoices;

public record InvoiceResponse(
    Guid Id,
    Guid UserId,
    Guid ClientId,
    Guid? ProjectId,
    string InvoiceNumber,
    string Status,
    string LanguageCode,
    DateOnly IssueDate,
    DateOnly DueDate,
    DateTime? SentAt,
    string Currency,
    decimal SubtotalAmount,
    decimal VatAmount,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal AmountDue,
    string? Notes,
    List<LineItemResponse> LineItems,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
