using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using pm.Application.Interfaces;
using pm.Application.Settings;
using pm.Domain.Entities;

namespace pm.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly SmtpSettings _settings;

    public SmtpEmailService(IOptions<SmtpSettings> settings)
    {
        _settings = settings.Value;
    }

    public Task SendInvoiceAsync(Invoice invoice, Client client, User sender)
    {
        var recipientName = ResolveRecipientName(client);
        var senderName = $"{sender.FirstName} {sender.LastName}".Trim();

        return SendAsync(
            to: (recipientName, client.Email),
            subject: $"Invoice {invoice.InvoiceNumber} from {senderName}",
            html: BuildInvoiceHtml(invoice, recipientName, senderName));
    }

    public Task SendInvoiceReminderAsync(Invoice invoice, Client client, User sender)
    {
        var recipientName = ResolveRecipientName(client);
        var senderName = $"{sender.FirstName} {sender.LastName}".Trim();
        var amountDue = invoice.TotalAmount - invoice.AmountPaid;

        return SendAsync(
            to: (recipientName, client.Email),
            subject: $"Payment reminder: invoice {invoice.InvoiceNumber}",
            html: BuildReminderHtml(invoice, recipientName, senderName, amountDue));
    }

    private async Task SendAsync((string name, string address) to, string subject, string html)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(new MailboxAddress(to.name, to.address));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(_settings.Username, _settings.Password);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
    }

    private static string ResolveRecipientName(Client client) =>
        client.ClientType == "company"
            ? client.CompanyName ?? client.Email
            : $"{client.FirstName} {client.LastName}".Trim();

    private static string BuildLineItemRows(Invoice invoice) =>
        string.Join("\n", invoice.LineItems.Select(li => $"""
                    <tr>
                        <td style="padding:8px;border-bottom:1px solid #eee">{li.Description}</td>
                        <td style="padding:8px;border-bottom:1px solid #eee;text-align:right">{li.Quantity:G}</td>
                        <td style="padding:8px;border-bottom:1px solid #eee;text-align:right">{li.UnitPrice:F2}</td>
                        <td style="padding:8px;border-bottom:1px solid #eee;text-align:right">{li.VatRate:G}%</td>
                        <td style="padding:8px;border-bottom:1px solid #eee;text-align:right">{li.LineTotal:F2}</td>
                    </tr>
            """));

    private static string BuildInvoiceHtml(Invoice invoice, string recipientName, string senderName) => $"""
        <html><body style="font-family:sans-serif;color:#222;max-width:600px;margin:0 auto;padding:24px">
        <p>Dear {recipientName},</p>
        <p>Please find your invoice <strong>{invoice.InvoiceNumber}</strong> below.</p>
        {BuildLineItemTable(invoice)}
        <p><strong>Due date:</strong> {invoice.DueDate:yyyy-MM-dd}</p>
        <p>Thank you for your business.</p>
        <p>{senderName}</p>
        </body></html>
        """;

    private static string BuildReminderHtml(Invoice invoice, string recipientName, string senderName, decimal amountDue) => $"""
        <html><body style="font-family:sans-serif;color:#222;max-width:600px;margin:0 auto;padding:24px">
        <p>Dear {recipientName},</p>
        <p>This is a friendly reminder that invoice <strong>{invoice.InvoiceNumber}</strong> is still outstanding.</p>
        <p style="font-size:1.05em"><strong>Amount due: {amountDue:F2} {invoice.Currency}</strong><br>
        Due date: {invoice.DueDate:yyyy-MM-dd}</p>
        {BuildLineItemTable(invoice)}
        <p>If you have already sent payment, please disregard this message.</p>
        <p>{senderName}</p>
        </body></html>
        """;

    private static string BuildLineItemTable(Invoice invoice) => $"""
        <table style="width:100%;border-collapse:collapse;margin:24px 0">
            <thead>
                <tr style="background:#f5f5f5">
                    <th style="padding:8px;text-align:left">Description</th>
                    <th style="padding:8px;text-align:right">Qty</th>
                    <th style="padding:8px;text-align:right">Unit price</th>
                    <th style="padding:8px;text-align:right">VAT</th>
                    <th style="padding:8px;text-align:right">Total ({invoice.Currency})</th>
                </tr>
            </thead>
            <tbody>
                {BuildLineItemRows(invoice)}
            </tbody>
            <tfoot>
                <tr>
                    <td colspan="4" style="padding:8px;text-align:right"><strong>Subtotal</strong></td>
                    <td style="padding:8px;text-align:right">{invoice.SubtotalAmount:F2} {invoice.Currency}</td>
                </tr>
                <tr>
                    <td colspan="4" style="padding:8px;text-align:right"><strong>VAT</strong></td>
                    <td style="padding:8px;text-align:right">{invoice.VatAmount:F2} {invoice.Currency}</td>
                </tr>
                <tr style="font-size:1.05em">
                    <td colspan="4" style="padding:8px;text-align:right"><strong>Total due</strong></td>
                    <td style="padding:8px;text-align:right"><strong>{invoice.TotalAmount:F2} {invoice.Currency}</strong></td>
                </tr>
            </tfoot>
        </table>
        """;
}
