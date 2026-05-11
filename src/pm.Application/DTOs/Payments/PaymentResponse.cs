namespace pm.Application.DTOs.Payments;

public record PaymentResponse(
    Guid Id,
    Guid InvoiceId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string Provider,
    string ProviderPaymentId,
    string Status,
    DateTime CreatedAt);

