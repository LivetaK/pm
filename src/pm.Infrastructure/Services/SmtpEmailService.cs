using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using pm.Application.DTOs.InvoicePdfs;
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

    public Task SendInvoiceAsync(
        Invoice invoice,
        Client client,
        User sender,
        InvoicePdfDownloadResponse pdf,
        string paymentLinkUrl)
    {
        var recipientName = ResolveRecipientName(client);
        var senderName = $"{sender.FirstName} {sender.LastName}".Trim();

        return SendAsync(
            to: (recipientName, client.Email),
            subject: $"Sąskaita {invoice.InvoiceNumber} nuo {senderName}",
            html: BuildInvoiceHtml(invoice, recipientName, senderName, paymentLinkUrl),
            pdf: pdf);
    }

    public Task SendInvoiceReminderAsync(Invoice invoice, Client client, User sender, string? paymentLinkUrl)
    {
        var recipientName = ResolveRecipientName(client);
        var senderName = $"{sender.FirstName} {sender.LastName}".Trim();
        var amountDue = invoice.TotalAmount - invoice.AmountPaid;

        return SendAsync(
            to: (recipientName, client.Email),
            subject: $"Priminimas apmokėti sąskaitą {invoice.InvoiceNumber}",
            html: BuildReminderHtml(invoice, recipientName, senderName, amountDue, paymentLinkUrl),
            pdf: null);
    }

    private async Task SendAsync((string name, string address) to, string subject, string html, InvoicePdfDownloadResponse? pdf)
    {
        if (string.IsNullOrWhiteSpace(_settings.Host) ||
            string.IsNullOrWhiteSpace(_settings.FromAddress) ||
            string.IsNullOrWhiteSpace(_settings.Username) ||
            string.IsNullOrWhiteSpace(_settings.Password))
        {
            throw new InvalidOperationException("SMTP is not configured.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(new MailboxAddress(to.name, to.address));
        message.Subject = subject;

        var body = new BodyBuilder { HtmlBody = html };
        if (pdf is not null)
            body.Attachments.Add(pdf.FileName, pdf.Content, ContentType.Parse(pdf.ContentType));

        message.Body = body.ToMessageBody();

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

    private static string BuildInvoiceHtml(Invoice invoice, string recipientName, string senderName, string paymentLinkUrl) => $"""
        <html><body style="font-family:sans-serif;color:#222;max-width:600px;margin:0 auto;padding:24px">
        <p>Sveiki, {recipientName},</p>
        <p>Prisegame sąskaitą <strong>{invoice.InvoiceNumber}</strong>.</p>
        {BuildLineItemTable(invoice)}
        <p><strong>Apmokėti iki:</strong> {invoice.DueDate:yyyy-MM-dd}</p>
        <p><a href="{paymentLinkUrl}" style="display:inline-block;background:#14532d;color:white;padding:12px 16px;text-decoration:none;border-radius:4px">Apmokėti sąskaitą</a></p>
        <p>{senderName}</p>
        </body></html>
        """;

    private static string BuildReminderHtml(Invoice invoice, string recipientName, string senderName, decimal amountDue, string? paymentLinkUrl) => $"""
        <html><body style="font-family:sans-serif;color:#222;max-width:600px;margin:0 auto;padding:24px">
        <p>Sveiki, {recipientName},</p>
        <p>Primename, kad sąskaita <strong>{invoice.InvoiceNumber}</strong> dar nėra apmokėta.</p>
        <p style="font-size:1.05em"><strong>Mokėtina suma: {amountDue:F2} {invoice.Currency}</strong><br>
        Apmokėti iki: {invoice.DueDate:yyyy-MM-dd}</p>
        {BuildLineItemTable(invoice)}
        {BuildPaymentLink(paymentLinkUrl)}
        <p>Jei jau apmokėjote, šį laišką galite ignoruoti.</p>
        <p>{senderName}</p>
        </body></html>
        """;

    private static string BuildPaymentLink(string? paymentLinkUrl) =>
        string.IsNullOrWhiteSpace(paymentLinkUrl)
            ? string.Empty
            : $"""<p><a href="{paymentLinkUrl}" style="display:inline-block;background:#14532d;color:white;padding:12px 16px;text-decoration:none;border-radius:4px">Apmokėti sąskaitą</a></p>""";

    private static string BuildLineItemTable(Invoice invoice) => $"""
        <table style="width:100%;border-collapse:collapse;margin:24px 0">
            <thead>
                <tr style="background:#f5f5f5">
                    <th style="padding:8px;text-align:left">Aprašymas</th>
                    <th style="padding:8px;text-align:right">Kiekis</th>
                    <th style="padding:8px;text-align:right">Kaina</th>
                    <th style="padding:8px;text-align:right">VAT</th>
                    <th style="padding:8px;text-align:right">Suma ({invoice.Currency})</th>
                </tr>
            </thead>
            <tbody>
                {BuildLineItemRows(invoice)}
            </tbody>
            <tfoot>
                <tr>
                    <td colspan="4" style="padding:8px;text-align:right"><strong>Suma be PVM</strong></td>
                    <td style="padding:8px;text-align:right">{invoice.SubtotalAmount:F2} {invoice.Currency}</td>
                </tr>
                <tr>
                    <td colspan="4" style="padding:8px;text-align:right"><strong>PVM</strong></td>
                    <td style="padding:8px;text-align:right">{invoice.VatAmount:F2} {invoice.Currency}</td>
                </tr>
                <tr style="font-size:1.05em">
                    <td colspan="4" style="padding:8px;text-align:right"><strong>Iš viso</strong></td>
                    <td style="padding:8px;text-align:right"><strong>{invoice.TotalAmount:F2} {invoice.Currency}</strong></td>
                </tr>
            </tfoot>
        </table>
        """;
}
