using Dapper;
using pm.Application.Interfaces;
using pm.Domain.Entities;

namespace pm.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly DapperContext _context;

    public UserRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        using var conn = _context.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            """
            SELECT id, email,
                   password_hash        AS PasswordHash,
                   is_email_verified    AS IsEmailVerified,
                   is_active            AS IsActive,
                   full_name            AS FullName,
                   phone,
                   preferred_language   AS PreferredLanguage,
                   timezone,
                   avatar_url           AS AvatarUrl,
                   company_name         AS CompanyName,
                   company_code         AS CompanyCode,
                   vat_code             AS VatCode,
                   address_line1        AS AddressLine1,
                   address_line2        AS AddressLine2,
                   city,
                   postal_code          AS PostalCode,
                   country_code         AS CountryCode,
                   default_currency     AS DefaultCurrency,
                   default_payment_terms_days AS DefaultPaymentTermsDays,
                   last_login_at        AS LastLoginAt,
                   created_at           AS CreatedAt,
                   updated_at           AS UpdatedAt,
                   deleted_at           AS DeletedAt
            FROM users WHERE id = @Id AND deleted_at IS NULL
            """, new { Id = id });
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        using var conn = _context.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<User>(
            """
            SELECT id, email,
                   password_hash        AS PasswordHash,
                   is_email_verified    AS IsEmailVerified,
                   is_active            AS IsActive,
                   full_name            AS FullName,
                   phone,
                   preferred_language   AS PreferredLanguage,
                   timezone,
                   avatar_url           AS AvatarUrl,
                   company_name         AS CompanyName,
                   company_code         AS CompanyCode,
                   vat_code             AS VatCode,
                   address_line1        AS AddressLine1,
                   address_line2        AS AddressLine2,
                   city,
                   postal_code          AS PostalCode,
                   country_code         AS CountryCode,
                   default_currency     AS DefaultCurrency,
                   default_payment_terms_days AS DefaultPaymentTermsDays,
                   last_login_at        AS LastLoginAt,
                   created_at           AS CreatedAt,
                   updated_at           AS UpdatedAt,
                   deleted_at           AS DeletedAt
            FROM users WHERE email = @Email AND deleted_at IS NULL
            """, new { Email = email });
    }

    public async Task<User> CreateAsync(User user)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            INSERT INTO users (id, email, password_hash, is_email_verified, is_active,
                               full_name, created_at, updated_at)
            VALUES (@Id, @Email, @PasswordHash, @IsEmailVerified, @IsActive,
                    @FullName, @CreatedAt, @UpdatedAt)
            """, user);
        return user;
    }

    public async Task UpdateAsync(User user)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE users
            SET full_name          = @FullName,
                phone              = @Phone,
                preferred_language = @PreferredLanguage,
                timezone           = @Timezone,
                updated_at         = @UpdatedAt
            WHERE id = @Id
            """, user);
    }

    public async Task UpdateLastLoginAsync(Guid userId, DateTime loginAt)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE users SET last_login_at = @LoginAt WHERE id = @Id",
            new { LoginAt = loginAt, Id = userId });
    }

    public async Task<UserSession?> GetSessionByTokenHashAsync(string tokenHash)
    {
        using var conn = _context.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<UserSession>(
            """
            SELECT id,
                   user_id              AS UserId,
                   refresh_token_hash   AS RefreshTokenHash,
                   user_agent           AS UserAgent,
                   ip_address           AS IpAddress,
                   expires_at           AS ExpiresAt,
                   revoked_at           AS RevokedAt,
                   created_at           AS CreatedAt
            FROM user_sessions WHERE refresh_token_hash = @TokenHash
            """, new { TokenHash = tokenHash });
    }

    public async Task CreateSessionAsync(UserSession session)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            INSERT INTO user_sessions (id, user_id, refresh_token_hash, user_agent, expires_at, created_at)
            VALUES (@Id, @UserId, @RefreshTokenHash, @UserAgent, @ExpiresAt, @CreatedAt)
            """, session);
    }

    public async Task RevokeSessionAsync(string tokenHash)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE user_sessions SET revoked_at = NOW() WHERE refresh_token_hash = @TokenHash",
            new { TokenHash = tokenHash });
    }

    public async Task RevokeAllUserSessionsAsync(Guid userId)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE user_sessions SET revoked_at = NOW() WHERE user_id = @UserId AND revoked_at IS NULL",
            new { UserId = userId });
    }
}
