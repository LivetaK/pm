namespace pm.Application.DTOs.Invoices;

public record LineItemResponse(
    Guid Id,
    int SortOrder,
    string Description,
    decimal Quantity,
    string? Unit,
    decimal UnitPrice,
    decimal VatRate,
    decimal LineSubtotal,
    decimal LineVatAmount,
    decimal LineTotal
);
