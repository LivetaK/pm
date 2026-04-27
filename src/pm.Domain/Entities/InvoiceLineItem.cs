namespace pm.Domain.Entities;

public class InvoiceLineItem
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public int SortOrder { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public string? Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal LineSubtotal { get; set; }
    public decimal LineVatAmount { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime CreatedAt { get; set; }
}
