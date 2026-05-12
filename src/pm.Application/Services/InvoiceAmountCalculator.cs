using pm.Application.DTOs.Invoices;
using pm.Domain.Entities;

namespace pm.Application.Services;

public static class InvoiceAmountCalculator
{
    public static InvoiceLineItem BuildLineItem(Guid invoiceId, LineItemRequest request, int fallbackSortOrder)
    {
        var subtotal = Math.Round(request.Quantity * request.UnitPrice, 2, MidpointRounding.AwayFromZero);
        var vatAmount = Math.Round(subtotal * request.VatRate / 100, 2, MidpointRounding.AwayFromZero);

        return new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            SortOrder = request.SortOrder == 0 ? fallbackSortOrder : request.SortOrder,
            Description = request.Description,
            Quantity = request.Quantity,
            Unit = request.Unit,
            UnitPrice = request.UnitPrice,
            VatRate = request.VatRate,
            LineSubtotal = subtotal,
            LineVatAmount = vatAmount,
            LineTotal = subtotal + vatAmount,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static (decimal subtotal, decimal vatAmount, decimal total) ComputeTotals(IEnumerable<InvoiceLineItem> items)
    {
        var subtotal = items.Sum(x => x.LineSubtotal);
        var vatAmount = items.Sum(x => x.LineVatAmount);
        return (subtotal, vatAmount, subtotal + vatAmount);
    }

    public static decimal GetFinalTotal(Invoice invoice) => invoice.TotalAmount;

    public static int ToMinorCurrencyUnits(Invoice invoice) =>
        (int)Math.Round(GetFinalTotal(invoice) * 100, 0, MidpointRounding.AwayFromZero);
}
