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
                    id, email, password_hash, is_email_verified, is_active,
                    first_name, last_name, phone,
                    preferred_language, timezone,
                    default_currency, default_payment_terms_days,
                    created_at, updated_at
                )
                VALUES (
                    @Id, @Email, @PasswordHash, @IsEmailVerified, @IsActive,
                    @FirstName, @LastName, @Phone,
                    @PreferredLanguage, @Timezone,
                    @DefaultCurrency, @DefaultPaymentTermsDays,
                    @CreatedAt, @UpdatedAt
                )
                ON CONFLICT (id) DO UPDATE
                SET email                       = EXCLUDED.email,
                    password_hash               = EXCLUDED.password_hash,
                    is_email_verified           = EXCLUDED.is_email_verified,
                    is_active                   = EXCLUDED.is_active,
                    first_name                  = EXCLUDED.first_name,
                    last_name                   = EXCLUDED.last_name,
                    phone                       = EXCLUDED.phone,
                    preferred_language          = EXCLUDED.preferred_language,
                    timezone                    = EXCLUDED.timezone,
                    default_currency            = EXCLUDED.default_currency,
                    default_payment_terms_days  = EXCLUDED.default_payment_terms_days,
                    updated_at                  = EXCLUDED.updated_at
                """,
                user);
        }

        foreach (var client in DemoClients)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO clients (
                    id, user_id, client_type,
                    first_name, last_name, company_name,
                    company_code, vat_code,
                    email, phone, bank_iban,
                    address_line1, address_line2, city, postal_code, country_code,
                    notes, is_active, created_at, updated_at
                )
                VALUES (
                    @Id, @UserId, @ClientType::client_type,
                    @FirstName, @LastName, @CompanyName,
                    @CompanyCode, @VatCode,
                    @Email, @Phone, @BankIban,
                    @AddressLine1, @AddressLine2, @City, @PostalCode, @CountryCode,
                    @Notes, @IsActive, @CreatedAt, @UpdatedAt
                )
                ON CONFLICT (id) DO UPDATE
                SET client_type     = EXCLUDED.client_type,
                    first_name      = EXCLUDED.first_name,
                    last_name       = EXCLUDED.last_name,
                    company_name    = EXCLUDED.company_name,
                    company_code    = EXCLUDED.company_code,
                    vat_code        = EXCLUDED.vat_code,
                    email           = EXCLUDED.email,
                    phone           = EXCLUDED.phone,
                    bank_iban       = EXCLUDED.bank_iban,
                    address_line1   = EXCLUDED.address_line1,
                    address_line2   = EXCLUDED.address_line2,
                    city            = EXCLUDED.city,
                    postal_code     = EXCLUDED.postal_code,
                    country_code    = EXCLUDED.country_code,
                    notes           = EXCLUDED.notes,
                    is_active       = EXCLUDED.is_active,
                    updated_at      = EXCLUDED.updated_at
                """,
                client);
        }

        foreach (var session in DemoSessions)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO user_sessions (
                    id, user_id, refresh_token_hash, user_agent, expires_at, revoked_at, created_at
                )
                VALUES (
                    @Id, @UserId, @RefreshTokenHash, @UserAgent, @ExpiresAt, @RevokedAt, @CreatedAt
                )
                ON CONFLICT (id) DO UPDATE
                SET user_id             = EXCLUDED.user_id,
                    refresh_token_hash  = EXCLUDED.refresh_token_hash,
                    user_agent          = EXCLUDED.user_agent,
                    expires_at          = EXCLUDED.expires_at,
                    revoked_at          = EXCLUDED.revoked_at,
                    created_at          = EXCLUDED.created_at
                """,
                session);
        }
    }

    private static IReadOnlyList<DemoUserSeed> DemoUsers => new[]
    {
        new DemoUserSeed(
            Id:                     Guid.Parse("f5af0dbc-b5d3-43cc-a265-5372cbf1a3c8"),
            Email:                  "roberta.demo@pm.local",
            PasswordHash:           BCrypt.Net.BCrypt.HashPassword("PmDemo123!"),
            FirstName:              "Roberta",
            LastName:               "Demo",
            Phone:                  "+37061234567",
            PreferredLanguage:      "lt",
            Timezone:               "Europe/Vilnius",
            DefaultCurrency:        "EUR",
            DefaultPaymentTermsDays: 14,
            CreatedAt:              SeedTimestampUtc),

        new DemoUserSeed(
            Id:                     Guid.Parse("efed3d76-369d-4993-a5ba-41752ebd94f9"),
            Email:                  "liveta.demo@pm.local",
            PasswordHash:           BCrypt.Net.BCrypt.HashPassword("PmDemo123!"),
            FirstName:              "Liveta",
            LastName:               "Demo",
            Phone:                  "+37062345678",
            PreferredLanguage:      "en",
            Timezone:               "Europe/Vilnius",
            DefaultCurrency:        "EUR",
            DefaultPaymentTermsDays: 30,
            CreatedAt:              SeedTimestampUtc.AddMinutes(5))
    };

    private static IReadOnlyList<DemoClientSeed> DemoClients => new[]
    {
        new DemoClientSeed(
            Id:          Guid.Parse("4423dc96-64da-4825-af57-900fb01c1d81"),
            UserId:      Guid.Parse("f5af0dbc-b5d3-43cc-a265-5372cbf1a3c8"),
            ClientType:  "company",
            FirstName:   null,
            LastName:    null,
            CompanyName: "UAB Vilniaus Video",
            CompanyCode: "302222222",
            VatCode:     "LT100001111111",
            Email:       "kontaktai@vilniausvideo.lt",
            Phone:       "+37060111111",
            BankIban:    null,
            AddressLine1: "Ukmerges g. 126",
            AddressLine2: "3 aukstas",
            City:        "Vilnius",
            PostalCode:  "08100",
            CountryCode: "LT",
            Notes:       "Pagrindinis demonstracinis klientas saskaitu ir projektu pristatymui.",
            IsActive:    true,
            CreatedAt:   SeedTimestampUtc.AddMinutes(10)),

        new DemoClientSeed(
            Id:          Guid.Parse("98bdd55c-eb75-44da-9d15-c1e7e91c79ff"),
            UserId:      Guid.Parse("f5af0dbc-b5d3-43cc-a265-5372cbf1a3c8"),
            ClientType:  "individual",
            FirstName:   "Austeja",
            LastName:    "Petrauskaite",
            CompanyName: null,
            CompanyCode: null,
            VatCode:     null,
            Email:       "austeja.petrauskaite@example.com",
            Phone:       "+37061112222",
            BankIban:    null,
            AddressLine1: "Smolensko g. 10",
            AddressLine2: null,
            City:        "Vilnius",
            PostalCode:  "03201",
            CountryCode: "LT",
            Notes:       "Naudinga parodyti individualaus kliento tipa.",
            IsActive:    true,
            CreatedAt:   SeedTimestampUtc.AddMinutes(20)),

        new DemoClientSeed(
            Id:          Guid.Parse("ddbcb14b-beb5-4507-85ec-bb50ce74b4b7"),
            UserId:      Guid.Parse("f5af0dbc-b5d3-43cc-a265-5372cbf1a3c8"),
            ClientType:  "company",
            FirstName:   null,
            LastName:    null,
            CompanyName: "MB Siaures Kryptis",
            CompanyCode: "304444444",
            VatCode:     null,
            Email:       "info@siaureskryptis.lt",
            Phone:       "+37063334444",
            BankIban:    null,
            AddressLine1: "Tilzes g. 88",
            AddressLine2: null,
            City:        "Siauliai",
            PostalCode:  "76295",
            CountryCode: "LT",
            Notes:       "Antras imones klientas saraso demonstracijai.",
            IsActive:    true,
            CreatedAt:   SeedTimestampUtc.AddMinutes(30)),

        new DemoClientSeed(
            Id:          Guid.Parse("6c60d78c-aa66-4c7e-b05d-db051cfa9800"),
            UserId:      Guid.Parse("efed3d76-369d-4993-a5ba-41752ebd94f9"),
            ClientType:  "company",
            FirstName:   null,
            LastName:    null,
            CompanyName: "UAB Baltic Growth",
            CompanyCode: "305777777",
            VatCode:     "LT100007777777",
            Email:       "accounts@balticgrowth.lt",
            Phone:       "+37064445555",
            BankIban:    null,
            AddressLine1: "J. Basanaviciaus g. 15",
            AddressLine2: null,
            City:        "Klaipeda",
            PostalCode:  "92125",
            CountryCode: "LT",
            Notes:       "Antras demo vartotojas su atskiru klientu sarasu.",
            IsActive:    true,
            CreatedAt:   SeedTimestampUtc.AddMinutes(40)),

        new DemoClientSeed(
            Id:          Guid.Parse("cc33a8fa-a96a-4502-821b-839701b71ebc"),
            UserId:      Guid.Parse("efed3d76-369d-4993-a5ba-41752ebd94f9"),
            ClientType:  "individual",
            FirstName:   "Jonas",
            LastName:    "Paulauskas",
            CompanyName: null,
            CompanyCode: null,
            VatCode:     null,
            Email:       "jonas.paulauskas@example.com",
            Phone:       "+37065556666",
            BankIban:    null,
            AddressLine1: "Taikos pr. 77",
            AddressLine2: null,
            City:        "Klaipeda",
            PostalCode:  "93262",
            CountryCode: "LT",
            Notes:       "Skirtas parodyti, kad kiekvienas vartotojas mato tik savo klientus.",
            IsActive:    true,
            CreatedAt:   SeedTimestampUtc.AddMinutes(50))
    };

    private static IReadOnlyList<DemoSessionSeed> DemoSessions => new[]
    {
        new DemoSessionSeed(
            Id:              Guid.Parse("49d4b096-3e06-4adf-9758-09728d93fdaf"),
            UserId:          Guid.Parse("f5af0dbc-b5d3-43cc-a265-5372cbf1a3c8"),
            RawRefreshToken: "pm-demo-refresh-roberta-2026",
            UserAgent:       "Presentation Seed",
            ExpiresAt:       SeedTimestampUtc.AddYears(1),
            CreatedAt:       SeedTimestampUtc.AddMinutes(12)),

        new DemoSessionSeed(
            Id:              Guid.Parse("d4292123-911f-4b89-a757-cee481ce2fbb"),
            UserId:          Guid.Parse("efed3d76-369d-4993-a5ba-41752ebd94f9"),
            RawRefreshToken: "pm-demo-refresh-liveta-2026",
            UserAgent:       "Presentation Seed",
            ExpiresAt:       SeedTimestampUtc.AddYears(1),
            CreatedAt:       SeedTimestampUtc.AddMinutes(42))
    };

    // -------------------------------------------------------------------------

    private sealed record DemoUserSeed(
        Guid Id,
        string Email,
        string PasswordHash,
        string FirstName,
        string LastName,
        string? Phone,
        string PreferredLanguage,
        string Timezone,
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
        bool IsActive,
        DateTime CreatedAt)
    {
        public DateTime UpdatedAt => CreatedAt;
    }

    private sealed record DemoSessionSeed(
        Guid Id,
        Guid UserId,
        string RawRefreshToken,
        string UserAgent,
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
