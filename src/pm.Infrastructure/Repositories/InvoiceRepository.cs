using Dapper;
using pm.Application.Interfaces;
using pm.Domain.Entities;

namespace pm.Infrastructure.Repositories;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly DapperContext _context;

    public InvoiceRepository(DapperContext context)
    {
        _context = context;
    }

    private const string InvoiceSelectColumns = """
        i.id,
        i.user_id           AS UserId,
        i.client_id         AS ClientId,
        i.project_id        AS ProjectId,
        i.invoice_number    AS InvoiceNumber,
        i.status::text      AS Status,
        i.language_code     AS LanguageCode,
        i.issue_date        AS IssueDate,
        i.due_date          AS DueDate,
        i.sent_at           AS SentAt,
        i.currency,
        i.subtotal_amount   AS SubtotalAmount,
        i.vat_amount        AS VatAmount,
        i.total_amount      AS TotalAmount,
        i.amount_paid       AS AmountPaid,
        i.notes,
        i.created_at        AS CreatedAt,
        i.updated_at        AS UpdatedAt,
        i.deleted_at        AS DeletedAt
        """;

    public async Task<IReadOnlyList<Invoice>> GetAllByUserIdAsync(Guid userId)
    {
        using var conn = _context.CreateConnection();
        var invoices = await conn.QueryAsync<Invoice>(
            $"""
            SELECT {InvoiceSelectColumns}
            FROM invoices i
            WHERE i.user_id = @UserId AND i.deleted_at IS NULL
            ORDER BY i.created_at DESC
            """,
            new { UserId = userId });

        var invoiceList = invoices.ToList();
        if (invoiceList.Count == 0) return invoiceList;

        var ids = invoiceList.Select(x => x.Id).ToArray();
        var lineItems = await conn.QueryAsync<InvoiceLineItem>(
            """
            SELECT id, invoice_id AS InvoiceId, sort_order AS SortOrder,
                   description, quantity, unit, unit_price AS UnitPrice,
                   vat_rate AS VatRate, line_subtotal AS LineSubtotal,
                   line_vat_amount AS LineVatAmount, line_total AS LineTotal,
                   created_at AS CreatedAt
            FROM invoice_line_items
            WHERE invoice_id = ANY(@Ids)
            ORDER BY invoice_id, sort_order
            """,
            new { Ids = ids });

        var itemsLookup = lineItems.ToLookup(x => x.InvoiceId);
        foreach (var inv in invoiceList)
            inv.LineItems = itemsLookup[inv.Id].ToList();

        return invoiceList;
    }

    public async Task<Invoice?> GetByIdAsync(Guid userId, Guid id)
    {
        using var conn = _context.CreateConnection();
        var invoice = await conn.QuerySingleOrDefaultAsync<Invoice>(
            $"""
            SELECT {InvoiceSelectColumns}
            FROM invoices i
            WHERE i.id = @Id AND i.user_id = @UserId AND i.deleted_at IS NULL
            """,
            new { Id = id, UserId = userId });

        if (invoice is null) return null;

        var lineItems = await conn.QueryAsync<InvoiceLineItem>(
            """
            SELECT id, invoice_id AS InvoiceId, sort_order AS SortOrder,
                   description, quantity, unit, unit_price AS UnitPrice,
                   vat_rate AS VatRate, line_subtotal AS LineSubtotal,
                   line_vat_amount AS LineVatAmount, line_total AS LineTotal,
                   created_at AS CreatedAt
            FROM invoice_line_items
            WHERE invoice_id = @InvoiceId
            ORDER BY sort_order
            """,
            new { InvoiceId = id });

        invoice.LineItems = lineItems.ToList();
        return invoice;
    }

    public async Task<Invoice> CreateAsync(Invoice invoice, IReadOnlyList<InvoiceLineItem> lineItems)
    {
        using var conn = _context.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(
            """
            INSERT INTO invoices (
                id, user_id, client_id, project_id,
                invoice_number, status, language_code,
                issue_date, due_date, currency,
                subtotal_amount, vat_amount, total_amount, amount_paid,
                notes, created_at, updated_at
            ) VALUES (
                @Id, @UserId, @ClientId, @ProjectId,
                @InvoiceNumber, @Status::invoice_status, @LanguageCode,
                @IssueDate, @DueDate, @Currency,
                @SubtotalAmount, @VatAmount, @TotalAmount, @AmountPaid,
                @Notes, @CreatedAt, @UpdatedAt
            )
            """, invoice, tx);

        await InsertLineItemsAsync(conn, lineItems, tx);

        tx.Commit();
        return invoice;
    }

    public async Task UpdateAsync(Invoice invoice, IReadOnlyList<InvoiceLineItem> lineItems)
    {
        using var conn = _context.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(
            """
            UPDATE invoices SET
                client_id       = @ClientId,
                project_id      = @ProjectId,
                language_code   = @LanguageCode,
                issue_date      = @IssueDate,
                due_date        = @DueDate,
                currency        = @Currency,
                subtotal_amount = @SubtotalAmount,
                vat_amount      = @VatAmount,
                total_amount    = @TotalAmount,
                notes           = @Notes,
                updated_at      = @UpdatedAt
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """, invoice, tx);

        await conn.ExecuteAsync(
            "DELETE FROM invoice_line_items WHERE invoice_id = @InvoiceId",
            new { InvoiceId = invoice.Id }, tx);

        await InsertLineItemsAsync(conn, lineItems, tx);

        tx.Commit();
    }

    public async Task<int> GetInvoiceCountForYearAsync(Guid userId, int year)
    {
        using var conn = _context.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM invoices
            WHERE user_id = @UserId
              AND EXTRACT(YEAR FROM issue_date) = @Year
              AND deleted_at IS NULL
            """,
            new { UserId = userId, Year = year });
    }

    public async Task AddStatusHistoryAsync(Guid invoiceId, Guid changedByUserId, string? fromStatus, string toStatus)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            INSERT INTO invoice_status_history (id, invoice_id, changed_by_user_id, from_status, to_status, created_at)
            VALUES (@Id, @InvoiceId, @ChangedByUserId,
                    @FromStatus::invoice_status,
                    @ToStatus::invoice_status,
                    NOW())
            """,
            new
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoiceId,
                ChangedByUserId = changedByUserId,
                FromStatus = fromStatus,
                ToStatus = toStatus
            });
    }

    private static async Task InsertLineItemsAsync(System.Data.IDbConnection conn,
        IReadOnlyList<InvoiceLineItem> lineItems, System.Data.IDbTransaction tx)
    {
        await conn.ExecuteAsync(
            """
            INSERT INTO invoice_line_items (
                id, invoice_id, sort_order, description,
                quantity, unit, unit_price, vat_rate,
                line_subtotal, line_vat_amount, line_total, created_at
            ) VALUES (
                @Id, @InvoiceId, @SortOrder, @Description,
                @Quantity, @Unit, @UnitPrice, @VatRate,
                @LineSubtotal, @LineVatAmount, @LineTotal, @CreatedAt
            )
            """, lineItems, tx);
    }
}
