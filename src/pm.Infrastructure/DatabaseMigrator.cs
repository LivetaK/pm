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
            CREATE EXTENSION IF NOT EXISTS "pgcrypto";
            CREATE EXTENSION IF NOT EXISTS "citext";

            CREATE OR REPLACE FUNCTION set_updated_at()
            RETURNS TRIGGER LANGUAGE plpgsql AS $$
            BEGIN
              NEW.updated_at = NOW();
              RETURN NEW;
            END;
            $$;

            -- USERS
            CREATE TABLE IF NOT EXISTS users (
                id                          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
                email                       CITEXT      NOT NULL,
                password_hash               VARCHAR(255) NOT NULL,
                is_email_verified           BOOLEAN     NOT NULL DEFAULT FALSE,
                is_active                   BOOLEAN     NOT NULL DEFAULT TRUE,
                first_name                  VARCHAR(100) NOT NULL,
                last_name                   VARCHAR(100) NOT NULL,
                phone                       VARCHAR(50),
                preferred_language          VARCHAR(10)  NOT NULL DEFAULT 'lt',
                timezone                    VARCHAR(60)  NOT NULL DEFAULT 'Europe/Vilnius',
                default_currency            CHAR(3)      NOT NULL DEFAULT 'EUR',
                default_payment_terms_days  INT          NOT NULL DEFAULT 14,
                last_login_at               TIMESTAMPTZ,
                created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                deleted_at                  TIMESTAMPTZ,
                CONSTRAINT uq_users_email UNIQUE (email)
            );

            CREATE INDEX IF NOT EXISTS idx_users_email
                ON users (email) WHERE deleted_at IS NULL;

            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_trigger
                    WHERE tgname = 'trg_users_updated_at'
                ) THEN
                    CREATE TRIGGER trg_users_updated_at
                        BEFORE UPDATE ON users
                        FOR EACH ROW EXECUTE FUNCTION set_updated_at();
                END IF;
            END $$;

            -- USER SESSIONS
            CREATE TABLE IF NOT EXISTS user_sessions (
                id                  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id             UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                refresh_token_hash  VARCHAR(255) NOT NULL,
                user_agent          TEXT,
                ip_address          INET,
                expires_at          TIMESTAMPTZ NOT NULL,
                revoked_at          TIMESTAMPTZ,
                created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                CONSTRAINT uq_user_sessions_token UNIQUE (refresh_token_hash)
            );

            CREATE INDEX IF NOT EXISTS idx_sessions_user_id
                ON user_sessions (user_id);
            CREATE INDEX IF NOT EXISTS idx_sessions_expires_at
                ON user_sessions (expires_at) WHERE revoked_at IS NULL;

            -- CLIENTS
            CREATE TABLE IF NOT EXISTS clients (
                id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id         UUID        NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                client_type     VARCHAR(20) NOT NULL DEFAULT 'individual',
                first_name      VARCHAR(100),
                last_name       VARCHAR(100),
                company_name    VARCHAR(255),
                company_code    VARCHAR(64),
                vat_code        VARCHAR(64),
                email           CITEXT      NOT NULL,
                phone           VARCHAR(50),
                bank_iban       VARCHAR(34),
                address_line1   VARCHAR(255),
                address_line2   VARCHAR(255),
                city            VARCHAR(100),
                postal_code     VARCHAR(20),
                country_code    CHAR(2)     NOT NULL DEFAULT 'LT',
                notes           TEXT,
                is_active       BOOLEAN     NOT NULL DEFAULT TRUE,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                deleted_at      TIMESTAMPTZ,
                CONSTRAINT chk_clients_type CHECK (client_type IN ('individual', 'company')),
                CONSTRAINT chk_clients_has_name CHECK (
                    first_name IS NOT NULL OR company_name IS NOT NULL
                ),
                CONSTRAINT uq_clients_company_code UNIQUE (user_id, company_code)
            );

            CREATE INDEX IF NOT EXISTS idx_clients_user_id
                ON clients (user_id) WHERE deleted_at IS NULL;
            CREATE INDEX IF NOT EXISTS idx_clients_email
                ON clients (user_id, email) WHERE deleted_at IS NULL;

            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_trigger
                    WHERE tgname = 'trg_clients_updated_at'
                ) THEN
                    CREATE TRIGGER trg_clients_updated_at
                        BEFORE UPDATE ON clients
                        FOR EACH ROW EXECUTE FUNCTION set_updated_at();
                END IF;
            END $$;
            """);
    }
}
