namespace pm.Application.DTOs.Clients;

public record ClientResponse(
    Guid Id,
    Guid UserId,
    string ClientType,
    string Name,
    string? LegalName,
    string? Email,
    string? Phone,
    string? CompanyCode,
    string? VatCode,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? PostalCode,
    string? CountryCode,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);


