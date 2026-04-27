using pm.Application.DTOs.Invoices;
using pm.Application.Interfaces;
using pm.Domain.Entities;

namespace pm.Application.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IInvoiceRepository _invoiceRepository;

    public InvoiceService(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    public async Task<IReadOnlyList<InvoiceResponse>> GetAllAsync(Guid userId)
    {
        var invoices = await _invoiceRepository.GetAllByUserIdAsync(userId);
        return invoices.Select(MapToResponse).ToList();
    }

    public async Task<InvoiceResponse> GetByIdAsync(Guid userId, Guid id)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(userId, id)
            ?? throw new KeyNotFoundException("Invoice not found.");
        return MapToResponse(invoice);
    }

    public async Task<InvoiceResponse> CreateAsync(Guid userId, CreateInvoiceRequest request)
    {
        if (request.LineItems == null || request.LineItems.Count == 0)
            throw new InvalidOperationException("Invoice must have at least one line item.");

        var issueDate = request.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var invoiceNumber = await GenerateInvoiceNumberAsync(userId, issueDate.Year);
        var lineItems = BuildLineItems(Guid.Empty, request.LineItems);
        var (subtotal, vatAmount, total) = ComputeTotals(lineItems);

        var now = DateTime.UtcNow;
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClientId = request.ClientId,
            ProjectId = request.ProjectId,
            InvoiceNumber = invoiceNumber,
            Status = "draft",
            LanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode) ? "lt" : request.LanguageCode,
            IssueDate = issueDate,
            DueDate = request.DueDate,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "EUR" : request.Currency.ToUpperInvariant(),
            SubtotalAmount = subtotal,
            VatAmount = vatAmount,
            TotalAmount = total,
            AmountPaid = 0,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Assign invoice id to line items
        foreach (var li in lineItems) li.InvoiceId = invoice.Id;

        await _invoiceRepository.CreateAsync(invoice, lineItems);
        await _invoiceRepository.AddStatusHistoryAsync(invoice.Id, userId, null, invoice.Status);

        invoice.LineItems = lineItems;
        return MapToResponse(invoice);
    }

    public async Task<InvoiceResponse> UpdateAsync(Guid userId, Guid id, UpdateInvoiceRequest request)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(userId, id)
            ?? throw new KeyNotFoundException("Invoice not found.");

        if (invoice.Status is "paid" or "cancelled")
            throw new InvalidOperationException($"Cannot edit an invoice with status '{invoice.Status}'.");

        if (request.LineItems == null || request.LineItems.Count == 0)
            throw new InvalidOperationException("Invoice must have at least one line item.");

        var lineItems = BuildLineItems(invoice.Id, request.LineItems);
        var (subtotal, vatAmount, total) = ComputeTotals(lineItems);

        invoice.ClientId = request.ClientId;
        invoice.ProjectId = request.ProjectId;
        invoice.LanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode) ? invoice.LanguageCode : request.LanguageCode;
        invoice.IssueDate = request.IssueDate;
        invoice.DueDate = request.DueDate;
        invoice.Currency = string.IsNullOrWhiteSpace(request.Currency) ? invoice.Currency : request.Currency.ToUpperInvariant();
        invoice.SubtotalAmount = subtotal;
        invoice.VatAmount = vatAmount;
        invoice.TotalAmount = total;
        invoice.Notes = request.Notes;
        invoice.UpdatedAt = DateTime.UtcNow;

        await _invoiceRepository.UpdateAsync(invoice, lineItems);

        invoice.LineItems = lineItems;
        return MapToResponse(invoice);
    }

    private async Task<string> GenerateInvoiceNumberAsync(Guid userId, int year)
    {
        var count = await _invoiceRepository.GetInvoiceCountForYearAsync(userId, year);
        return $"INV-{year}-{count + 1:0000}";
    }

    private static List<InvoiceLineItem> BuildLineItems(Guid invoiceId, List<LineItemRequest> requests)
    {
        return requests.Select((r, i) =>
        {
            var subtotal = Math.Round(r.Quantity * r.UnitPrice, 2);
            var vatAmount = Math.Round(subtotal * r.VatRate / 100, 2);
            return new InvoiceLineItem
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoiceId,
                SortOrder = r.SortOrder == 0 ? i : r.SortOrder,
                Description = r.Description,
                Quantity = r.Quantity,
                Unit = r.Unit,
                UnitPrice = r.UnitPrice,
                VatRate = r.VatRate,
                LineSubtotal = subtotal,
                LineVatAmount = vatAmount,
                LineTotal = subtotal + vatAmount,
                CreatedAt = DateTime.UtcNow
            };
        }).ToList();
    }

    private static (decimal subtotal, decimal vatAmount, decimal total) ComputeTotals(List<InvoiceLineItem> items)
    {
        var subtotal = items.Sum(x => x.LineSubtotal);
        var vatAmount = items.Sum(x => x.LineVatAmount);
        return (subtotal, vatAmount, subtotal + vatAmount);
    }

    private static InvoiceResponse MapToResponse(Invoice i) =>
        new(i.Id, i.UserId, i.ClientId, i.ProjectId,
            i.InvoiceNumber, i.Status, i.LanguageCode,
            i.IssueDate, i.DueDate, i.SentAt,
            i.Currency, i.SubtotalAmount, i.VatAmount, i.TotalAmount,
            i.AmountPaid, i.TotalAmount - i.AmountPaid,
            i.Notes,
            i.LineItems.Select(li => new LineItemResponse(
                li.Id, li.SortOrder, li.Description, li.Quantity, li.Unit,
                li.UnitPrice, li.VatRate, li.LineSubtotal, li.LineVatAmount, li.LineTotal
            )).ToList(),
            i.CreatedAt, i.UpdatedAt);
}
