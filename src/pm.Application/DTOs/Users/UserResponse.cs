namespace pm.Application.DTOs.Users;

public record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string PreferredLanguage,
    string Timezone,
    string DefaultCurrency,
    int DefaultPaymentTermsDays,
    bool IsEmailVerified,
    DateTime CreatedAt
);
