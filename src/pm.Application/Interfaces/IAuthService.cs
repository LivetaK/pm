using pm.Application.DTOs.Auth;
using pm.Application.DTOs.Users;

namespace pm.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshAsync(RefreshRequest request);
    Task LogoutAsync(string refreshToken);
    Task<UserResponse> GetCurrentUserAsync(Guid userId);
    Task<UserResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
}
