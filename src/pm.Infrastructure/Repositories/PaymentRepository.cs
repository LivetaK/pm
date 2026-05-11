using Dapper;
using pm.Application.Interfaces;
using pm.Domain.Entities;

namespace pm.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly DapperContext _context;

    public PaymentRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(Payment payment)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            """
            INSERT INTO payments (id, invoice_id, user_id, amount, currency, provider, provider_payment_id, status, created_at)
            VALUES (@Id, @InvoiceId, @UserId, @Amount, @Currency, @Provider, @ProviderPaymentId, @Status, @CreatedAt)
            """, payment);
    }

    public async Task<IReadOnlyList<Payment>> GetByInvoiceIdAsync(Guid invoiceId)
    {
        using var conn = _context.CreateConnection();
        var rows = await conn.QueryAsync<Payment>(
            "SELECT id, invoice_id AS InvoiceId, user_id AS UserId, amount, currency, provider, provider_payment_id AS ProviderPaymentId, status, created_at AS CreatedAt FROM payments WHERE invoice_id = @InvoiceId ORDER BY created_at",
            new { InvoiceId = invoiceId });
        return rows.ToList();
    }

    public async Task<Payment?> GetByProviderPaymentIdAsync(string providerPaymentId)
    {
        using var conn = _context.CreateConnection();
        var row = await conn.QuerySingleOrDefaultAsync<Payment>(
            "SELECT id, invoice_id AS InvoiceId, user_id AS UserId, amount, currency, provider, provider_payment_id AS ProviderPaymentId, status, created_at AS CreatedAt FROM payments WHERE provider_payment_id = @ProviderPaymentId",
            new { ProviderPaymentId = providerPaymentId });
        return row;
    }

    public async Task UpdateStatusAsync(string providerPaymentId, string status)
    {
        using var conn = _context.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE payments SET status = @Status WHERE provider_payment_id = @ProviderPaymentId",
            new { ProviderPaymentId = providerPaymentId, Status = status });
    }
}

