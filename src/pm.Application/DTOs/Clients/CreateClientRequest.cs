namespace pm.Application.DTOs.Clients;

public record CreateClientRequest(
    string? ClientType,
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
    string? Notes
);


