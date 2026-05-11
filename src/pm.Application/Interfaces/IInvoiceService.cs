using pm.Application.DTOs.Invoices;

namespace pm.Application.Interfaces;

public interface IInvoiceService
{
    Task<IReadOnlyList<InvoiceResponse>> GetAllAsync(Guid userId);
    Task<InvoiceResponse> GetByIdAsync(Guid userId, Guid id);
    Task<InvoiceResponse> CreateAsync(Guid userId, CreateInvoiceRequest request);
    Task<InvoiceResponse> UpdateAsync(Guid userId, Guid id, UpdateInvoiceRequest request);
    Task<InvoiceResponse> SendAsync(Guid userId, Guid id);
    Task<InvoiceResponse> SendReminderAsync(Guid userId, Guid id);
    Task<string> CreatePaymentLinkAsync(Guid userId, Guid id);
}
