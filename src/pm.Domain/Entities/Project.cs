namespace pm.Domain.Entities;

public class Project
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AgreedScope { get; set; }
    public string Status { get; set; } = "draft";
    public string PricingType { get; set; } = "fixed";
    public decimal? AgreedAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal VatRate { get; set; } = 21.00m;
    public int PaymentTermsDays { get; set; } = 14;
    public DateOnly? StartsOn { get; set; }
    public DateOnly? DueOn { get; set; }
    public DateTime? WorkCompletedAt { get; set; }
    public DateTime? InvoicedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
