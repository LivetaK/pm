using pm.Domain.Entities;

namespace pm.Application.Interfaces;

public interface IInvoiceRepository
{
    Task<IReadOnlyList<Invoice>> GetAllByUserIdAsync(Guid userId);
    Task<Invoice?> GetByIdAsync(Guid userId, Guid id);
    Task<Invoice> CreateAsync(Invoice invoice, IReadOnlyList<InvoiceLineItem> lineItems);
    Task UpdateAsync(Invoice invoice, IReadOnlyList<InvoiceLineItem> lineItems);
    Task<int> GetInvoiceCountForYearAsync(Guid userId, int year);
    Task AddStatusHistoryAsync(Guid invoiceId, Guid changedByUserId, string? fromStatus, string toStatus);
    Task MarkSentAsync(Guid userId, Guid id, DateTime sentAt);
    Task MarkAsPaidAsync(Guid userId, Guid id, decimal amount, DateTime paidAt);
}
