using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using pm.Application.DTOs.Auth;
using pm.Application.DTOs.Users;
using pm.Application.Interfaces;
using pm.Application.Settings;
using pm.Domain.Entities;

namespace pm.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptions<JwtSettings> jwtSettings)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existing = await _userRepository.GetByEmailAsync(request.Email.ToLowerInvariant());
        if (existing != null)
            throw new InvalidOperationException("Email already in use.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName,
            IsActive = true,
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(user);
        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email.ToLowerInvariant())
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is inactive.");

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        await _userRepository.UpdateLastLoginAsync(user.Id, DateTime.UtcNow);
        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var session = await _userRepository.GetSessionByTokenHashAsync(tokenHash)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        if (session.IsRevoked)
            throw new UnauthorizedAccessException("Refresh token expired or revoked.");

        await _userRepository.RevokeSessionAsync(tokenHash);

        var user = await _userRepository.GetByIdAsync(session.UserId)
            ?? throw new UnauthorizedAccessException("User not found.");

        return await IssueTokensAsync(user);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        await _userRepository.RevokeSessionAsync(HashToken(refreshToken));
    }

    public async Task<UserResponse> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");
        return MapToResponse(user);
    }

    public async Task<UserResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        user.FullName = request.FullName;
        user.Phone = request.Phone;
        user.PreferredLanguage = request.PreferredLanguage;
        user.Timezone = request.Timezone;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);
        return MapToResponse(user);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var rawRefreshToken = _tokenService.GenerateRefreshToken();

        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RefreshTokenHash = HashToken(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.CreateSessionAsync(session);

        return new AuthResponse(
            accessToken,
            rawRefreshToken,
            DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes)
        );
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static UserResponse MapToResponse(User user) =>
        new(user.Id, user.Email, user.FullName, user.Phone,
            user.PreferredLanguage, user.Timezone, user.IsEmailVerified, user.CreatedAt);
}
