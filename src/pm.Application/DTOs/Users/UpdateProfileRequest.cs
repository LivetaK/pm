namespace pm.Application.DTOs.Users;

public record UpdateProfileRequest(
    string FullName,
    string? Phone,
    string? PreferredLanguage,
    string? Timezone
);
