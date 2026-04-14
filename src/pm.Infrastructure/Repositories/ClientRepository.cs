using Dapper;
using pm.Application.Interfaces;
using pm.Domain.Entities;

namespace pm.Infrastructure.Repositories;

public class ClientRepository : IClientRepository
{
    private readonly DapperContext _context;

    public ClientRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Client>> GetAllByUserIdAsync(Guid userId)
    {
        using var conn = _context.CreateConnection();
        var clients = await conn.QueryAsync<Client>(
            """
            SELECT id,
                   user_id              AS UserId,
                   client_type          AS ClientType,
                   name                 AS Name,
                   legal_name           AS LegalName,
                   email                AS Email,
                   phone                AS Phone,
                   company_code         AS CompanyCode,
                   vat_code             AS VatCode,
                   address_line1        AS AddressLine1,
                   address_line2        AS AddressLine2,
                   city                 AS City,
                   postal_code          AS PostalCode,
                   country_code         AS CountryCode,
                   notes                AS Notes,
                   is_active            AS IsActive,
                   created_at           AS CreatedAt,
                   updated_at           AS UpdatedAt,
                   deleted_at           AS DeletedAt
            FROM clients
            WHERE user_id = @UserId AND deleted_at IS NULL
            ORDER BY created_at DESC
            """,
            new { UserId = userId });

        return clients.ToList();
    }

    public async Task<Client?> GetByIdAsync(Guid userId, Guid id)
    {
        using var conn = _context.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Client>(
            """
            SELECT id,
                   user_id              AS UserId,
                   client_type          AS ClientType,
                   name                 AS Name,
                   legal_name           AS LegalName,
                   email                AS Email,
                   phone                AS Phone,
                   company_code         AS CompanyCode,
                   vat_code             AS VatCode,
                   address_line1        AS AddressLine1,
                   address_line2        AS AddressLine2,
                   city                 AS City,
                   postal_code          AS PostalCode,
                   country_code         AS CountryCode,
                   notes                AS Notes,
                   is_active            AS IsActive,
                   created_at           AS CreatedAt,
                   updated_at           AS UpdatedAt,
                   deleted_at           AS DeletedAt
            FROM clients
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """,
            new { Id = id, UserId = userId });
    }

    public async Task<Client> CreateAsync(Client client)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            INSERT INTO clients (
                id, user_id, client_type, name, legal_name, email, phone,
                company_code, vat_code, address_line1, address_line2, city,
                postal_code, country_code, notes, is_active, created_at, updated_at
            )
            VALUES (
                @Id, @UserId, @ClientType, @Name, @LegalName, @Email, @Phone,
                @CompanyCode, @VatCode, @AddressLine1, @AddressLine2, @City,
                @PostalCode, @CountryCode, @Notes, @IsActive, @CreatedAt, @UpdatedAt
            )
            """,
            client);

        return client;
    }

    public async Task UpdateAsync(Client client)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE clients
            SET client_type = @ClientType,
                name = @Name,
                legal_name = @LegalName,
                email = @Email,
                phone = @Phone,
                company_code = @CompanyCode,
                vat_code = @VatCode,
                address_line1 = @AddressLine1,
                address_line2 = @AddressLine2,
                city = @City,
                postal_code = @PostalCode,
                country_code = @CountryCode,
                notes = @Notes,
                is_active = @IsActive,
                updated_at = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """,
            client);
    }

    public async Task SoftDeleteAsync(Guid userId, Guid id)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE clients
            SET deleted_at = NOW(),
                is_active = false,
                updated_at = NOW()
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """,
            new { Id = id, UserId = userId });
    }
}


