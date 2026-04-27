namespace pm.Domain.Entities;

public class Invoice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ClientId { get; set; }
    public Guid? ProjectId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    public string LanguageCode { get; set; } = "lt";
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTime? SentAt { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal SubtotalAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public List<InvoiceLineItem> LineItems { get; set; } = new();
}
