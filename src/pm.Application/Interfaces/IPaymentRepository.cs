using pm.Domain.Entities;

namespace pm.Application.Interfaces;

public interface IPaymentRepository
{
    Task CreateAsync(Payment payment);
    Task<IReadOnlyList<Payment>> GetByInvoiceIdAsync(Guid invoiceId);
    Task<Payment?> GetByProviderPaymentIdAsync(string providerPaymentId);
    Task UpdateStatusAsync(string providerPaymentId, string status);
}

