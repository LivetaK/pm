namespace pm.Application.DTOs.Users;

public record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? Phone,
    string? PreferredLanguage,
    string? Timezone
);
