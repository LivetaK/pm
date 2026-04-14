namespace pm.Domain.Entities;

public class Client
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ClientType { get; set; } = "individual";
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CompanyCode { get; set; }
    public string? VatCode { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string CountryCode { get; set; } = "LT";
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}


