namespace pm.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public bool IsActive { get; set; } = true;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? PreferredLanguage { get; set; }
    public string? Timezone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyCode { get; set; }
    public string? VatCode { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
    public string? DefaultCurrency { get; set; }
    public int? DefaultPaymentTermsDays { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
