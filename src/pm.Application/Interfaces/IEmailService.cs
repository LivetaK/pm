using pm.Domain.Entities;
using pm.Application.DTOs.InvoicePdfs;

namespace pm.Application.Interfaces;

public interface IEmailService
{
    Task SendInvoiceAsync(
        Invoice invoice,
        Client client,
        User sender,
        InvoicePdfDownloadResponse pdf,
        string paymentLinkUrl);

    Task SendInvoiceReminderAsync(
        Invoice invoice,
        Client client,
        User sender,
        string? paymentLinkUrl);
}
