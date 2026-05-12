using pm.Application.DTOs.InvoicePdfs;
using pm.Domain.Entities;

namespace pm.Application.Interfaces;

public interface IInvoicePdfService
{
    Task<InvoicePdfReference> GenerateAsync(Invoice invoice, Client client, Project project, User seller);
    Task<InvoicePdfDownloadResponse> GetAsync(Invoice invoice);
}
