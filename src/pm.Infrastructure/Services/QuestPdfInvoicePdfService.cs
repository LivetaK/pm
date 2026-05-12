using Microsoft.Extensions.Options;
using pm.Application.DTOs.InvoicePdfs;
using pm.Application.Interfaces;
using pm.Application.Settings;
using pm.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace pm.Infrastructure.Services;

public class QuestPdfInvoicePdfService : IInvoicePdfService
{
    private const string ContentType = "application/pdf";
    private readonly string _storageRoot;

    public QuestPdfInvoicePdfService(IOptions<InvoicePdfSettings> settings)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        _storageRoot = string.IsNullOrWhiteSpace(settings.Value.StorageRoot)
            ? Path.Combine(Directory.GetCurrentDirectory(), "invoice-pdfs")
            : settings.Value.StorageRoot;
    }

    public Task<InvoicePdfReference> GenerateAsync(Invoice invoice, Client client, Project project, User seller)
    {
        var generatedAt = DateTime.UtcNow;
        var relativePath = Path.Combine(invoice.UserId.ToString("N"), $"{invoice.Id:N}.pdf");
        var fullPath = ResolveFullPath(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        BuildDocument(invoice, client, project, seller).GeneratePdf(fullPath);

        return Task.FromResult(new InvoicePdfReference(relativePath, generatedAt));
    }

    public async Task<InvoicePdfDownloadResponse> GetAsync(Invoice invoice)
    {
        if (string.IsNullOrWhiteSpace(invoice.PdfFilePath))
            throw new KeyNotFoundException("Invoice PDF not found.");

        var fullPath = ResolveFullPath(invoice.PdfFilePath);
        if (!File.Exists(fullPath))
            throw new KeyNotFoundException("Invoice PDF not found.");

        var bytes = await File.ReadAllBytesAsync(fullPath);
        var fileName = $"{invoice.InvoiceNumber}.pdf";
        return new InvoicePdfDownloadResponse(fileName, ContentType, bytes);
    }

    private static IDocument BuildDocument(Invoice invoice, Client client, Project project, User seller) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Column(column =>
                {
                    column.Item().Text("PVM sąskaita faktūra").FontSize(20).Bold();
                    column.Item().Text($"Sąskaitos Nr. {invoice.InvoiceNumber}");
                    column.Item().Text($"Išrašymo data: {FormatDate(invoice.IssueDate)}");
                    column.Item().Text($"Apmokėti iki: {FormatDate(invoice.DueDate)}");
                });

                page.Content().PaddingTop(24).Column(column =>
                {
                    column.Spacing(16);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Element(c => PartyBlock(c, "Pardavėjas", FormatSellerName(seller), seller.Email, seller.Phone));
                        row.RelativeItem().Element(c => PartyBlock(c, "Pirkėjas", FormatClientName(client), client.Email, client.Phone));
                    });

                    column.Item().Text($"Projektas: {project.Name}").Bold();

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(32);
                            columns.RelativeColumn();
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(70);
                            columns.ConstantColumn(55);
                            columns.ConstantColumn(80);
                        });

                        table.Header(header =>
                        {
                            HeaderCell(header, "Nr.");
                            HeaderCell(header, "Aprašymas");
                            HeaderCell(header, "Kiekis");
                            HeaderCell(header, "Kaina");
                            HeaderCell(header, "PVM");
                            HeaderCell(header, "Suma");
                        });

                        foreach (var item in invoice.LineItems.OrderBy(x => x.SortOrder))
                        {
                            BodyCell(table, item.SortOrder.ToString());
                            BodyCell(table, item.Description);
                            BodyCell(table, $"{item.Quantity:0.##} {item.Unit}".Trim());
                            BodyCell(table, FormatMoney(item.UnitPrice, invoice.Currency));
                            BodyCell(table, $"{item.VatRate:0.##}%");
                            BodyCell(table, FormatMoney(item.LineTotal, invoice.Currency));
                        }
                    });

                    column.Item().AlignRight().Width(220).Column(totals =>
                    {
                        totals.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Suma be PVM:");
                            row.ConstantItem(90).AlignRight().Text(FormatMoney(invoice.SubtotalAmount, invoice.Currency));
                        });
                        totals.Item().Row(row =>
                        {
                            row.RelativeItem().Text("PVM:");
                            row.ConstantItem(90).AlignRight().Text(FormatMoney(invoice.VatAmount, invoice.Currency));
                        });
                        totals.Item().BorderTop(1).PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text("Iš viso:");
                            row.ConstantItem(90).AlignRight().Text(FormatMoney(invoice.TotalAmount, invoice.Currency)).Bold();
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(invoice.Notes))
                        column.Item().Text($"Pastabos: {invoice.Notes}");
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Sugeneruota sistemoje ");
                    text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"));
                });
            });
        });

    private string ResolveFullPath(string storedPath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, storedPath));
        var root = Path.GetFullPath(_storageRoot);
        if (fullPath != root && !fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid invoice PDF path.");

        return fullPath;
    }

    private static void PartyBlock(IContainer container, string title, string name, string email, string? phone)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(column =>
        {
            column.Item().Text(title).Bold();
            column.Item().Text(name);
            column.Item().Text(email);
            if (!string.IsNullOrWhiteSpace(phone))
                column.Item().Text(phone);
        });
    }

    private static void HeaderCell(TableCellDescriptor header, string text) =>
        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(text).Bold();

    private static void BodyCell(TableDescriptor table, string text) =>
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(text);

    private static string FormatSellerName(User seller) => $"{seller.FirstName} {seller.LastName}".Trim();

    private static string FormatClientName(Client client) =>
        client.ClientType == "company"
            ? client.CompanyName ?? string.Empty
            : $"{client.FirstName} {client.LastName}".Trim();

    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd");

    private static string FormatMoney(decimal amount, string currency) => $"{amount:0.00} {currency}";
}
