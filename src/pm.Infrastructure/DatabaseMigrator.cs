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

            -- ENUM TYPES (safe creation)
            DO $$ BEGIN
                CREATE TYPE project_status AS ENUM ('draft','active','completed','invoiced','paid','cancelled');
            EXCEPTION WHEN duplicate_object THEN NULL; END $$;

            DO $$ BEGIN
                CREATE TYPE project_pricing_type AS ENUM ('fixed','hourly');
            EXCEPTION WHEN duplicate_object THEN NULL; END $$;

            DO $$ BEGIN
                CREATE TYPE invoice_status AS ENUM ('draft','sent','partially_paid','paid','overdue','cancelled');
            EXCEPTION WHEN duplicate_object THEN NULL; END $$;

            -- PROJECTS
            CREATE TABLE IF NOT EXISTS projects (
                id                  UUID                    PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id             UUID                    NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                client_id           UUID                    NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,
                name                VARCHAR(200)            NOT NULL,
                description         TEXT,
                agreed_scope        TEXT,
                status              project_status          NOT NULL DEFAULT 'draft',
                pricing_type        project_pricing_type    NOT NULL DEFAULT 'fixed',
                agreed_amount       NUMERIC(12,2),
                currency            CHAR(3)                 NOT NULL DEFAULT 'EUR',
                vat_rate            NUMERIC(5,2)            NOT NULL DEFAULT 21.00,
                payment_terms_days  INT                     NOT NULL DEFAULT 14,
                starts_on           DATE,
                due_on              DATE,
                work_completed_at   TIMESTAMPTZ,
                invoiced_at         TIMESTAMPTZ,
                completed_at        TIMESTAMPTZ,
                cancelled_at        TIMESTAMPTZ,
                created_at          TIMESTAMPTZ             NOT NULL DEFAULT NOW(),
                updated_at          TIMESTAMPTZ             NOT NULL DEFAULT NOW(),
                deleted_at          TIMESTAMPTZ
            );

            CREATE INDEX IF NOT EXISTS idx_projects_user_id
                ON projects (user_id) WHERE deleted_at IS NULL;
            CREATE INDEX IF NOT EXISTS idx_projects_client_id
                ON projects (client_id) WHERE deleted_at IS NULL;
            CREATE INDEX IF NOT EXISTS idx_projects_status
                ON projects (user_id, status) WHERE deleted_at IS NULL;

            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'trg_projects_updated_at') THEN
                    CREATE TRIGGER trg_projects_updated_at
                        BEFORE UPDATE ON projects
                        FOR EACH ROW EXECUTE FUNCTION set_updated_at();
                END IF;
            END $$;

            -- PROJECT STATUS HISTORY
            CREATE TABLE IF NOT EXISTS project_status_history (
                id                  UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
                project_id          UUID            NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
                changed_by_user_id  UUID            NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                from_status         project_status,
                to_status           project_status  NOT NULL,
                reason              TEXT,
                created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_psh_project_id
                ON project_status_history (project_id, created_at DESC);

            -- INVOICES
            CREATE TABLE IF NOT EXISTS invoices (
                id                  UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
                user_id             UUID            NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                client_id           UUID            NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,
                project_id          UUID            REFERENCES projects(id) ON DELETE SET NULL,
                invoice_number      VARCHAR(50)     NOT NULL,
                status              invoice_status  NOT NULL DEFAULT 'draft',
                language_code       VARCHAR(10)     NOT NULL DEFAULT 'lt',
                issue_date          DATE            NOT NULL DEFAULT CURRENT_DATE,
                due_date            DATE            NOT NULL,
                sent_at             TIMESTAMPTZ,
                currency            CHAR(3)         NOT NULL DEFAULT 'EUR',
                subtotal_amount     NUMERIC(12,2)   NOT NULL DEFAULT 0,
                vat_amount          NUMERIC(12,2)   NOT NULL DEFAULT 0,
                total_amount        NUMERIC(12,2)   NOT NULL DEFAULT 0,
                amount_paid         NUMERIC(12,2)   NOT NULL DEFAULT 0,
                amount_due          NUMERIC(12,2)   GENERATED ALWAYS AS (total_amount - amount_paid) STORED,
                notes               TEXT,
                created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
                updated_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
                deleted_at          TIMESTAMPTZ,
                CONSTRAINT uq_invoices_number UNIQUE (user_id, invoice_number)
            );

            CREATE INDEX IF NOT EXISTS idx_invoices_user_id
                ON invoices (user_id) WHERE deleted_at IS NULL;
            CREATE INDEX IF NOT EXISTS idx_invoices_client_id
                ON invoices (client_id) WHERE deleted_at IS NULL;
            CREATE INDEX IF NOT EXISTS idx_invoices_project_id
                ON invoices (project_id) WHERE deleted_at IS NULL;
            CREATE INDEX IF NOT EXISTS idx_invoices_status
                ON invoices (user_id, status, due_date) WHERE deleted_at IS NULL;

            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'trg_invoices_updated_at') THEN
                    CREATE TRIGGER trg_invoices_updated_at
                        BEFORE UPDATE ON invoices
                        FOR EACH ROW EXECUTE FUNCTION set_updated_at();
                END IF;
            END $$;

            -- INVOICE STATUS HISTORY
            CREATE TABLE IF NOT EXISTS invoice_status_history (
                id                  UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
                invoice_id          UUID            NOT NULL REFERENCES invoices(id) ON DELETE CASCADE,
                changed_by_user_id  UUID            NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                from_status         invoice_status,
                to_status           invoice_status  NOT NULL,
                reason              TEXT,
                created_at          TIMESTAMPTZ     NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_ish_invoice_id
                ON invoice_status_history (invoice_id, created_at DESC);

            -- INVOICE LINE ITEMS
            CREATE TABLE IF NOT EXISTS invoice_line_items (
                id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
                invoice_id      UUID            NOT NULL REFERENCES invoices(id) ON DELETE CASCADE,
                sort_order      INT             NOT NULL DEFAULT 0,
                description     TEXT            NOT NULL,
                quantity        NUMERIC(12,2)   NOT NULL DEFAULT 1,
                unit            VARCHAR(50),
                unit_price      NUMERIC(12,2)   NOT NULL,
                vat_rate        NUMERIC(5,2)    NOT NULL DEFAULT 0,
                line_subtotal   NUMERIC(12,2)   NOT NULL,
                line_vat_amount NUMERIC(12,2)   NOT NULL,
                line_total      NUMERIC(12,2)   NOT NULL,
                created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW()
            );

            CREATE INDEX IF NOT EXISTS idx_line_items_invoice_id
                ON invoice_line_items (invoice_id, sort_order);
            """);
    }
}
