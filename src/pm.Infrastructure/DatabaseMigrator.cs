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
            """);
    }
}
