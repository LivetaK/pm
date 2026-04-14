using System.Security.Cryptography;
using System.Text;
using Dapper;

namespace pm.Infrastructure;

public class DemoDataSeeder
{
    private static readonly DateTime SeedTimestampUtc = new(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);
    private readonly DapperContext _context;

    public DemoDataSeeder(DapperContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        using var conn = _context.CreateConnection();

        foreach (var user in DemoUsers)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO users (
                    id, email, password_hash, is_email_verified, is_active, full_name, phone,
                    preferred_language, timezone, company_name, company_code, vat_code,
                    address_line1, address_line2, city, postal_code, country_code,
                    default_currency, default_payment_terms_days, created_at, updated_at
                )
                VALUES (
                    @Id, @Email, @PasswordHash, @IsEmailVerified, @IsActive, @FullName, @Phone,
                    @PreferredLanguage, @Timezone, @CompanyName, @CompanyCode, @VatCode,
                    @AddressLine1, @AddressLine2, @City, @PostalCode, @CountryCode,
                    @DefaultCurrency, @DefaultPaymentTermsDays, @CreatedAt, @UpdatedAt
                )
                ON CONFLICT (id) DO UPDATE
                SET email = EXCLUDED.email,
                    password_hash = EXCLUDED.password_hash,
                    is_email_verified = EXCLUDED.is_email_verified,
                    is_active = EXCLUDED.is_active,
                    full_name = EXCLUDED.full_name,
                    phone = EXCLUDED.phone,
                    preferred_language = EXCLUDED.preferred_language,
                    timezone = EXCLUDED.timezone,
                    company_name = EXCLUDED.company_name,
                    company_code = EXCLUDED.company_code,
                    vat_code = EXCLUDED.vat_code,
                    address_line1 = EXCLUDED.address_line1,
                    address_line2 = EXCLUDED.address_line2,
                    city = EXCLUDED.city,
                    postal_code = EXCLUDED.postal_code,
                    country_code = EXCLUDED.country_code,
                    default_currency = EXCLUDED.default_currency,
                    default_payment_terms_days = EXCLUDED.default_payment_terms_days,
                    updated_at = EXCLUDED.updated_at
                """,
                user);
        }

        foreach (var client in DemoClients)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO clients (
                    id, user_id, client_type, name, legal_name, email, phone, company_code,
                    vat_code, address_line1, address_line2, city, postal_code, country_code,
                    notes, is_active, created_at, updated_at, deleted_at
                )
                VALUES (
                    @Id, @UserId, @ClientType, @Name, @LegalName, @Email, @Phone, @CompanyCode,
                    @VatCode, @AddressLine1, @AddressLine2, @City, @PostalCode, @CountryCode,
                    @Notes, @IsActive, @CreatedAt, @UpdatedAt, @DeletedAt
                )
                ON CONFLICT (id) DO UPDATE
                SET user_id = EXCLUDED.user_id,
                    client_type = EXCLUDED.client_type,
                    name = EXCLUDED.name,
                    legal_name = EXCLUDED.legal_name,
                    email = EXCLUDED.email,
                    phone = EXCLUDED.phone,
                    company_code = EXCLUDED.company_code,
                    vat_code = EXCLUDED.vat_code,
                    address_line1 = EXCLUDED.address_line1,
                    address_line2 = EXCLUDED.address_line2,
                    city = EXCLUDED.city,
                    postal_code = EXCLUDED.postal_code,
                    country_code = EXCLUDED.country_code,
                    notes = EXCLUDED.notes,
                    is_active = EXCLUDED.is_active,
                    updated_at = EXCLUDED.updated_at,
                    deleted_at = EXCLUDED.deleted_at
                """,
                client);
        }

        foreach (var session in DemoSessions)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO user_sessions (
                    id, user_id, refresh_token_hash, user_agent, ip_address, expires_at, revoked_at, created_at
                )
                VALUES (
                    @Id, @UserId, @RefreshTokenHash, @UserAgent, @IpAddress, @ExpiresAt, @RevokedAt, @CreatedAt
                )
                ON CONFLICT (id) DO UPDATE
                SET user_id = EXCLUDED.user_id,
                    refresh_token_hash = EXCLUDED.refresh_token_hash,
                    user_agent = EXCLUDED.user_agent,
                    ip_address = EXCLUDED.ip_address,
                    expires_at = EXCLUDED.expires_at,
                    revoked_at = EXCLUDED.revoked_at,
                    created_at = EXCLUDED.created_at
                """,
                session);
        }
    }

    private static IReadOnlyList<DemoUserSeed> DemoUsers => new[]
    {
        new DemoUserSeed(
            Guid.Parse("f5af0dbc-b5d3-43cc-a265-5372cbf1a3c8"),
            "roberta.demo@pm.local",
            BCrypt.Net.BCrypt.HashPassword("PmDemo123!"),
            "Roberta Demo",
            "+37061234567",
            "lt",
            "Europe/Vilnius",
            "Roberta Design Studio",
            "305555555",
            "LT100009999999",
            "Gedimino pr. 1",
            null,
            "Vilnius",
            "01103",
            "LT",
            "EUR",
            14,
            SeedTimestampUtc),
        new DemoUserSeed(
            Guid.Parse("efed3d76-369d-4993-a5ba-41752ebd94f9"),
            "liveta.demo@pm.local",
            BCrypt.Net.BCrypt.HashPassword("PmDemo123!"),
            "Liveta Demo",
            "+37062345678",
            "en",
            "Europe/Vilnius",
            "Liveta Creative",
            "306666666",
            "LT100008888888",
            "Laisves al. 20",
            null,
            "Kaunas",
            "44240",
            "LT",
            "EUR",
            30,
            SeedTimestampUtc.AddMinutes(5))
    };

    private static IReadOnlyList<DemoClientSeed> DemoClients => new[]
    {
        new DemoClientSeed(
            Guid.Parse("4423dc96-64da-4825-af57-900fb01c1d81"),
            Guid.Parse("f5af0dbc-b5d3-43cc-a265-5372cbf1a3c8"),
            "company",
            "UAB Vilniaus Video",
            "UAB Vilniaus Video",
            "kontaktai@vilniausvideo.lt",
            "+37060111111",
            "302222222",
            "LT100001111111",
            "Ukmerges g. 126",
            "3 aukstas",
            "Vilnius",
            "08100",
            "LT",
            "Pagrindinis demonstracinis klientas saskaitu ir projektu pristatymui.",
            true,
            SeedTimestampUtc.AddMinutes(10)),
        new DemoClientSeed(
            Guid.Parse("98bdd55c-eb75-44da-9d15-c1e7e91c79ff"),
            Guid.Parse("f5af0dbc-b5d3-43cc-a265-5372cbf1a3c8"),
            "individual",
            "Austeja Petrauskaite",
            null,
            "austeja.petrauskaite@example.com",
            "+37061112222",
            null,
            null,
            "Smolensko g. 10",
            null,
            "Vilnius",
            "03201",
            "LT",
            "Naudinga parodyti individualaus kliento tipa.",
            true,
            SeedTimestampUtc.AddMinutes(20)),
        new DemoClientSeed(
            Guid.Parse("ddbcb14b-beb5-4507-85ec-bb50ce74b4b7"),
            Guid.Parse("f5af0dbc-b5d3-43cc-a265-5372cbf1a3c8"),
            "company",
            "MB Siaures Kryptis",
            "MB Siaures Kryptis",
            "info@siaureskryptis.lt",
            "+37063334444",
            "304444444",
            null,
            "Tilzes g. 88",
            null,
            "Siauliai",
            "76295",
            "LT",
            "Antras imones klientas sąrašo demonstracijai.",
            true,
            SeedTimestampUtc.AddMinutes(30)),
        new DemoClientSeed(
            Guid.Parse("6c60d78c-aa66-4c7e-b05d-db051cfa9800"),
            Guid.Parse("efed3d76-369d-4993-a5ba-41752ebd94f9"),
            "company",
            "UAB Baltic Growth",
            "UAB Baltic Growth",
            "accounts@balticgrowth.lt",
            "+37064445555",
            "305777777",
            "LT100007777777",
            "J. Basanaviciaus g. 15",
            null,
            "Klaipeda",
            "92125",
            "LT",
            "Antras demo vartotojas su atskiru klientu sarasu.",
            true,
            SeedTimestampUtc.AddMinutes(40)),
        new DemoClientSeed(
            Guid.Parse("cc33a8fa-a96a-4502-821b-839701b71ebc"),
            Guid.Parse("efed3d76-369d-4993-a5ba-41752ebd94f9"),
            "individual",
            "Jonas Paulauskas",
            null,
            "jonas.paulauskas@example.com",
            "+37065556666",
            null,
            null,
            "Taikos pr. 77",
            null,
            "Klaipeda",
            "93262",
            "LT",
            "Skirtas parodyti, kad kiekvienas vartotojas mato tik savo klientus.",
            true,
            SeedTimestampUtc.AddMinutes(50))
    };

    private static IReadOnlyList<DemoSessionSeed> DemoSessions => new[]
    {
        new DemoSessionSeed(
            Guid.Parse("49d4b096-3e06-4adf-9758-09728d93fdaf"),
            Guid.Parse("f5af0dbc-b5d3-43cc-a265-5372cbf1a3c8"),
            "pm-demo-refresh-roberta-2026",
            "Presentation Seed",
            "127.0.0.1",
            SeedTimestampUtc.AddYears(1),
            SeedTimestampUtc.AddMinutes(12)),
        new DemoSessionSeed(
            Guid.Parse("d4292123-911f-4b89-a757-cee481ce2fbb"),
            Guid.Parse("efed3d76-369d-4993-a5ba-41752ebd94f9"),
            "pm-demo-refresh-liveta-2026",
            "Presentation Seed",
            "127.0.0.1",
            SeedTimestampUtc.AddYears(1),
            SeedTimestampUtc.AddMinutes(42))
    };

    private sealed record DemoUserSeed(
        Guid Id,
        string Email,
        string PasswordHash,
        string FullName,
        string Phone,
        string PreferredLanguage,
        string Timezone,
        string CompanyName,
        string CompanyCode,
        string VatCode,
        string AddressLine1,
        string? AddressLine2,
        string City,
        string PostalCode,
        string CountryCode,
        string DefaultCurrency,
        int DefaultPaymentTermsDays,
        DateTime CreatedAt)
    {
        public bool IsEmailVerified => true;
        public bool IsActive => true;
        public DateTime UpdatedAt => CreatedAt;
    }

    private sealed record DemoClientSeed(
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
        string CountryCode,
        string? Notes,
        bool IsActive,
        DateTime CreatedAt)
    {
        public DateTime UpdatedAt => CreatedAt;
        public DateTime? DeletedAt => null;
    }

    private sealed record DemoSessionSeed(
        Guid Id,
        Guid UserId,
        string RawRefreshToken,
        string UserAgent,
        string IpAddress,
        DateTime ExpiresAt,
        DateTime CreatedAt)
    {
        public string RefreshTokenHash => HashToken(RawRefreshToken);
        public DateTime? RevokedAt => null;

        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
