using Dapper;

namespace pm.Infrastructure;

public class DatabaseMigrator
{
    private readonly DapperContext _context;

    public DatabaseMigrator(DapperContext context)
    {
        _context = context;
    }

    public async Task MigrateAsync()
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync("""
            CREATE EXTENSION IF NOT EXISTS citext;

            CREATE TABLE IF NOT EXISTS users (
                id                          UUID PRIMARY KEY,
                email                       TEXT NOT NULL UNIQUE,
                password_hash               TEXT NOT NULL,
                is_email_verified           BOOLEAN NOT NULL DEFAULT FALSE,
                is_active                   BOOLEAN NOT NULL DEFAULT TRUE,
                full_name                   TEXT NOT NULL,
                phone                       TEXT,
                preferred_language          TEXT,
                timezone                    TEXT,
                avatar_url                  TEXT,
                company_name                TEXT,
                company_code                TEXT,
                vat_code                    TEXT,
                address_line1               TEXT,
                address_line2               TEXT,
                city                        TEXT,
                postal_code                 TEXT,
                country_code                TEXT,
                default_currency            TEXT,
                default_payment_terms_days  INT,
                last_login_at               TIMESTAMPTZ,
                created_at                  TIMESTAMPTZ NOT NULL,
                updated_at                  TIMESTAMPTZ NOT NULL,
                deleted_at                  TIMESTAMPTZ
            );

            CREATE TABLE IF NOT EXISTS user_sessions (
                id                  UUID PRIMARY KEY,
                user_id             UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                refresh_token_hash   TEXT NOT NULL UNIQUE,
                user_agent          TEXT,
                ip_address          TEXT,
                expires_at          TIMESTAMPTZ NOT NULL,
                revoked_at          TIMESTAMPTZ,
                created_at          TIMESTAMPTZ NOT NULL
            );

            CREATE TABLE IF NOT EXISTS clients (
                id                  UUID PRIMARY KEY,
                user_id             UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                client_type         VARCHAR(20) NOT NULL DEFAULT 'individual',
                name                VARCHAR(200) NOT NULL,
                legal_name          VARCHAR(255),
                email               CITEXT,
                phone               VARCHAR(50),
                company_code        VARCHAR(50),
                vat_code            VARCHAR(50),
                address_line1       VARCHAR(255),
                address_line2       VARCHAR(255),
                city                VARCHAR(100),
                postal_code         VARCHAR(20),
                country_code        CHAR(2) NOT NULL DEFAULT 'LT',
                notes               TEXT,
                is_active           BOOLEAN NOT NULL DEFAULT TRUE,
                created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                deleted_at          TIMESTAMPTZ,
                CONSTRAINT chk_clients_type CHECK (client_type IN ('individual', 'company')),
                CONSTRAINT chk_clients_country_code CHECK (country_code ~ '^[A-Z]{2}$')
            );

            ALTER TABLE users
                ADD COLUMN IF NOT EXISTS avatar_url           TEXT,
                ADD COLUMN IF NOT EXISTS phone               TEXT,
                ADD COLUMN IF NOT EXISTS preferred_language  TEXT,
                ADD COLUMN IF NOT EXISTS timezone            TEXT,
                ADD COLUMN IF NOT EXISTS company_name        TEXT,
                ADD COLUMN IF NOT EXISTS company_code        TEXT,
                ADD COLUMN IF NOT EXISTS vat_code            TEXT,
                ADD COLUMN IF NOT EXISTS address_line1       TEXT,
                ADD COLUMN IF NOT EXISTS address_line2       TEXT,
                ADD COLUMN IF NOT EXISTS city                TEXT,
                ADD COLUMN IF NOT EXISTS postal_code         TEXT,
                ADD COLUMN IF NOT EXISTS country_code        TEXT,
                ADD COLUMN IF NOT EXISTS default_currency    TEXT,
                ADD COLUMN IF NOT EXISTS default_payment_terms_days INT,
                ADD COLUMN IF NOT EXISTS last_login_at       TIMESTAMPTZ,
                ADD COLUMN IF NOT EXISTS deleted_at          TIMESTAMPTZ;

            CREATE INDEX IF NOT EXISTS ix_user_sessions_user_id ON user_sessions(user_id);
            CREATE INDEX IF NOT EXISTS ix_clients_user_id ON clients(user_id);
            """);
    }
}
