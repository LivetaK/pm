using pm.Domain.Entities;

namespace pm.Application.Interfaces;

public interface IEmailService
{
    Task SendInvoiceAsync(Invoice invoice, Client client, User sender);
    Task SendInvoiceReminderAsync(Invoice invoice, Client client, User sender);
}
