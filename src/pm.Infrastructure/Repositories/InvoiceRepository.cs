using Dapper;
using Npgsql;
using pm.Application.Exceptions;
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
        i.payment_link_url  AS PaymentLinkUrl,
        i.payment_link_status AS PaymentLinkStatus,
        i.payment_link_generated_at AS PaymentLinkGeneratedAt,
        i.payment_link_deactivated_at AS PaymentLinkDeactivatedAt,
        i.payment_link_error AS PaymentLinkError,
        i.pdf_file_path     AS PdfFilePath,
        i.pdf_generated_at  AS PdfGeneratedAt,
        i.email_send_status AS EmailSendStatus,
        i.email_sent_at     AS EmailSentAt,
        i.email_last_error  AS EmailLastError,
        i.reminder_send_status AS ReminderSendStatus,
        i.reminder_last_sent_at AS ReminderLastSentAt,
        i.reminder_count    AS ReminderCount,
        i.reminder_last_error AS ReminderLastError,
        i.notes,
        i.created_at        AS CreatedAt,
        i.updated_at        AS UpdatedAt,
        i.deleted_at        AS DeletedAt
        """;

    public async Task<IReadOnlyList<Invoice>> GetAllByUserIdAsync(Guid userId)
    {
        using var conn = _context.CreateConnection();
        await MarkOverdueAsync(userId, DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow);

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

    public async Task<IReadOnlyList<Invoice>> GetOverdueReminderCandidatesAsync(Guid userId, DateOnly today)
    {
        using var conn = _context.CreateConnection();
        await MarkOverdueAsync(userId, today, DateTime.UtcNow);

        var invoices = await conn.QueryAsync<Invoice>(
            $"""
            SELECT {InvoiceSelectColumns}
            FROM invoices i
            WHERE i.user_id = @UserId
              AND i.deleted_at IS NULL
              AND i.status IN ('overdue'::invoice_status, 'partially_paid'::invoice_status)
              AND i.total_amount > i.amount_paid
              AND i.due_date < @Today
              AND (
                  i.reminder_last_sent_at IS NULL
                  OR i.reminder_last_sent_at < NOW() - INTERVAL '1 day'
              )
            ORDER BY i.due_date, i.created_at
            """,
            new { UserId = userId, Today = today });

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

    public async Task<Invoice?> GetByProjectIdAsync(Guid userId, Guid projectId)
    {
        using var conn = _context.CreateConnection();
        var invoice = await conn.QuerySingleOrDefaultAsync<Invoice>(
            $"""
            SELECT {InvoiceSelectColumns}
            FROM invoices i
            WHERE i.project_id = @ProjectId
              AND i.user_id = @UserId
              AND i.deleted_at IS NULL
            """,
            new { ProjectId = projectId, UserId = userId });

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
            new { InvoiceId = invoice.Id });

        invoice.LineItems = lineItems.ToList();
        return invoice;
    }

    public async Task<IReadOnlyList<Guid>> GetUserIdsWithOverdueInvoicesAsync(DateOnly today)
    {
        using var conn = _context.CreateConnection();
        var userIds = await conn.QueryAsync<Guid>(
            """
            SELECT DISTINCT user_id
            FROM invoices
            WHERE deleted_at IS NULL
              AND status IN ('sent'::invoice_status, 'overdue'::invoice_status, 'partially_paid'::invoice_status)
              AND due_date < @Today
              AND total_amount > amount_paid
            """,
            new { Today = today });

        return userIds.ToList();
    }

    public async Task<Invoice> CreateAsync(Invoice invoice, IReadOnlyList<InvoiceLineItem> lineItems)
    {
        using var conn = _context.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO invoices (
                    id, user_id, client_id, project_id,
                    invoice_number, status, language_code,
                    issue_date, due_date, currency,
                    subtotal_amount, vat_amount, total_amount, amount_paid,
                    payment_link_url, payment_link_status,
                    payment_link_generated_at, payment_link_deactivated_at, payment_link_error,
                    pdf_file_path, pdf_generated_at,
                    email_send_status, email_sent_at, email_last_error,
                    reminder_send_status, reminder_last_sent_at, reminder_count, reminder_last_error,
                    notes, created_at, updated_at
                ) VALUES (
                    @Id, @UserId, @ClientId, @ProjectId,
                    @InvoiceNumber, @Status::invoice_status, @LanguageCode,
                    @IssueDate, @DueDate, @Currency,
                    @SubtotalAmount, @VatAmount, @TotalAmount, @AmountPaid,
                    @PaymentLinkUrl, @PaymentLinkStatus,
                    @PaymentLinkGeneratedAt, @PaymentLinkDeactivatedAt, @PaymentLinkError,
                    @PdfFilePath, @PdfGeneratedAt,
                    @EmailSendStatus, @EmailSentAt, @EmailLastError,
                    @ReminderSendStatus, @ReminderLastSentAt, @ReminderCount, @ReminderLastError,
                    @Notes, @CreatedAt, @UpdatedAt
                )
                """, invoice, tx);

            await InsertLineItemsAsync(conn, lineItems, tx);

            tx.Commit();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            tx.Rollback();
            throw new ApiConflictException("Invoice number or project invoice already exists.");
        }

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

    public async Task<int> GetNextInvoiceSequenceAsync(Guid userId, int year)
    {
        using var conn = _context.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO invoice_number_counters (user_id, invoice_year, last_number)
            VALUES (@UserId, @Year, 1)
            ON CONFLICT (user_id, invoice_year)
            DO UPDATE SET last_number = invoice_number_counters.last_number + 1
            RETURNING last_number
            """,
            new { UserId = userId, Year = year });
    }

    public async Task SetPdfReferenceAsync(Guid userId, Guid id, string pdfFilePath, DateTime generatedAt)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE invoices
            SET pdf_file_path = @PdfFilePath,
                pdf_generated_at = @GeneratedAt,
                updated_at = @GeneratedAt
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """,
            new { Id = id, UserId = userId, PdfFilePath = pdfFilePath, GeneratedAt = generatedAt });
    }

    public async Task SavePaymentLinkAsync(Guid userId, Guid id, string paymentLinkUrl, DateTime generatedAt)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE invoices
            SET payment_link_url = @PaymentLinkUrl,
                payment_link_status = 'active',
                payment_link_generated_at = @GeneratedAt,
                payment_link_deactivated_at = NULL,
                payment_link_error = NULL,
                updated_at = @GeneratedAt
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """,
            new { Id = id, UserId = userId, PaymentLinkUrl = paymentLinkUrl, GeneratedAt = generatedAt });
    }

    public async Task RecordPaymentLinkFailureAsync(Guid userId, Guid id, string error, DateTime failedAt)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE invoices
            SET payment_link_status = 'failed',
                payment_link_error = @Error,
                updated_at = @FailedAt
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """,
            new { Id = id, UserId = userId, Error = error, FailedAt = failedAt });
    }

    public async Task DeactivatePaymentLinkAsync(Guid userId, Guid id, DateTime deactivatedAt)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE invoices
            SET payment_link_status = 'inactive',
                payment_link_deactivated_at = @DeactivatedAt,
                updated_at = @DeactivatedAt
            WHERE id = @Id
              AND user_id = @UserId
              AND deleted_at IS NULL
              AND payment_link_status = 'active'
            """,
            new { Id = id, UserId = userId, DeactivatedAt = deactivatedAt });
    }

    public async Task RecordInvoiceEmailResultAsync(Guid userId, Guid id, bool sent, string? error, DateTime attemptedAt)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE invoices
            SET email_send_status = @Status,
                email_sent_at = CASE WHEN @Sent THEN @AttemptedAt ELSE email_sent_at END,
                email_last_error = @Error,
                updated_at = @AttemptedAt
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """,
            new
            {
                Id = id,
                UserId = userId,
                Sent = sent,
                Status = sent ? "sent" : "failed",
                Error = error,
                AttemptedAt = attemptedAt
            });
    }

    public async Task RecordReminderEmailResultAsync(Guid userId, Guid id, bool sent, string? error, DateTime attemptedAt)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE invoices
            SET reminder_send_status = @Status,
                reminder_last_sent_at = CASE WHEN @Sent THEN @AttemptedAt ELSE reminder_last_sent_at END,
                reminder_count = CASE WHEN @Sent THEN reminder_count + 1 ELSE reminder_count END,
                reminder_last_error = @Error,
                updated_at = @AttemptedAt
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """,
            new
            {
                Id = id,
                UserId = userId,
                Sent = sent,
                Status = sent ? "sent" : "failed",
                Error = error,
                AttemptedAt = attemptedAt
            });
    }

    public async Task<int> MarkOverdueAsync(Guid userId, DateOnly today, DateTime updatedAt)
    {
        using var conn = _context.CreateConnection();
        return await conn.ExecuteAsync(
            """
            UPDATE invoices
            SET status = 'overdue'::invoice_status,
                updated_at = @UpdatedAt
            WHERE user_id = @UserId
              AND deleted_at IS NULL
              AND status = 'sent'::invoice_status
              AND due_date < @Today
              AND total_amount > amount_paid
            """,
            new { UserId = userId, Today = today, UpdatedAt = updatedAt });
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

    public async Task MarkSentAsync(Guid userId, Guid id, DateTime sentAt)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE invoices
            SET status = 'sent'::invoice_status, sent_at = @SentAt, updated_at = @SentAt
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """,
            new { Id = id, UserId = userId, SentAt = sentAt });
    }

    public async Task MarkAsPaidAsync(Guid userId, Guid id, decimal amount, DateTime paidAt)
    {
        using var conn = _context.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(
            """
            UPDATE invoices
            SET amount_paid = GREATEST(0, amount_paid + @Amount),
                status = CASE
                    WHEN GREATEST(0, amount_paid + @Amount) >= total_amount THEN 'paid'::invoice_status
                    WHEN GREATEST(0, amount_paid + @Amount) > 0 THEN 'partially_paid'::invoice_status
                    WHEN due_date < CURRENT_DATE THEN 'overdue'::invoice_status
                    ELSE status
                END,
                updated_at = @PaidAt
            WHERE id = @Id AND user_id = @UserId AND deleted_at IS NULL
            """,
            new { Id = id, UserId = userId, Amount = amount, PaidAt = paidAt }, tx);

        await AddStatusHistoryAsync(id, userId, null, "paid");

        tx.Commit();
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
