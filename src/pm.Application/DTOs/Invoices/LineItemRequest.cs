namespace pm.Application.DTOs.Invoices;

public record LineItemRequest(
    int SortOrder,
    string Description,
    decimal Quantity,
    string? Unit,
    decimal UnitPrice,
    decimal VatRate
);
