using pm.Domain.Entities;

namespace pm.Application.Interfaces;

public interface IInvoiceRepository
{
    Task<IReadOnlyList<Invoice>> GetAllByUserIdAsync(Guid userId);
    Task<Invoice?> GetByIdAsync(Guid userId, Guid id);
    Task<Invoice?> GetByProjectIdAsync(Guid userId, Guid projectId);
    Task<IReadOnlyList<Guid>> GetUserIdsWithOverdueInvoicesAsync(DateOnly today);
    Task<IReadOnlyList<Invoice>> GetOverdueReminderCandidatesAsync(Guid userId, DateOnly today);
    Task<Invoice> CreateAsync(Invoice invoice, IReadOnlyList<InvoiceLineItem> lineItems);
    Task UpdateAsync(Invoice invoice, IReadOnlyList<InvoiceLineItem> lineItems);
    Task<int> GetNextInvoiceSequenceAsync(Guid userId, int year);
    Task AddStatusHistoryAsync(Guid invoiceId, Guid changedByUserId, string? fromStatus, string toStatus);
    Task SetPdfReferenceAsync(Guid userId, Guid id, string pdfFilePath, DateTime generatedAt);
    Task SavePaymentLinkAsync(Guid userId, Guid id, string paymentLinkUrl, DateTime generatedAt);
    Task RecordPaymentLinkFailureAsync(Guid userId, Guid id, string error, DateTime failedAt);
    Task DeactivatePaymentLinkAsync(Guid userId, Guid id, DateTime deactivatedAt);
    Task RecordInvoiceEmailResultAsync(Guid userId, Guid id, bool sent, string? error, DateTime attemptedAt);
    Task RecordReminderEmailResultAsync(Guid userId, Guid id, bool sent, string? error, DateTime attemptedAt);
    Task<int> MarkOverdueAsync(Guid userId, DateOnly today, DateTime updatedAt);
    Task MarkSentAsync(Guid userId, Guid id, DateTime sentAt);
    Task MarkAsPaidAsync(Guid userId, Guid id, decimal amount, DateTime paidAt);
}
