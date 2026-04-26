namespace pm.Application.DTOs.Clients;

public record UpdateClientRequest(
    string ClientType,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? CompanyCode,
    string? VatCode,
    string Email,
    string? Phone,
    string? BankIban,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? PostalCode,
    string CountryCode,
    string? Notes,
    bool IsActive
);
