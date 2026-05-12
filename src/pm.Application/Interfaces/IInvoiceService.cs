using pm.Application.DTOs.Invoices;
using pm.Application.DTOs.InvoicePdfs;

namespace pm.Application.Interfaces;

public interface IInvoiceService
{
    Task<IReadOnlyList<InvoiceResponse>> GetAllAsync(Guid userId);
    Task<InvoiceResponse> GetByIdAsync(Guid userId, Guid id);
    Task<InvoiceResponse> CreateAsync(Guid userId, CreateInvoiceRequest request);
    Task<InvoiceResponse> CreateForCompletedProjectAsync(Guid userId, Guid projectId);
    Task<InvoiceResponse> UpdateAsync(Guid userId, Guid id, UpdateInvoiceRequest request);
    Task<InvoiceResponse> SendAsync(Guid userId, Guid id);
    Task<InvoiceResponse> SendReminderAsync(Guid userId, Guid id);
    Task<OverdueReminderProcessResponse> ProcessOverdueRemindersAsync(Guid userId);
    Task<string> CreatePaymentLinkAsync(Guid userId, Guid id);
    Task<InvoicePdfDownloadResponse> GetPdfAsync(Guid userId, Guid id);
}
