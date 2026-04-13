namespace pm.Application.DTOs.Users;

public record UserResponse(
    Guid Id,
    string Email,
    string FullName,
    string? Phone,
    string? PreferredLanguage,
    string? Timezone,
    bool IsEmailVerified,
    DateTime CreatedAt
);
