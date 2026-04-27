using Dapper;
using pm.Application.DTOs.Statistics;
using pm.Application.Interfaces;

namespace pm.Infrastructure.Repositories;

public class StatisticsRepository : IStatisticsRepository
{
    private readonly DapperContext _context;

    public StatisticsRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task<RevenueStatsResponse> GetRevenueAsync(
        Guid userId, DateTime? dateFrom, DateTime? dateTo, Guid? clientId)
    {
        using var conn = _context.CreateConnection();

        var byMonth = await conn.QueryAsync<RevenueDataPoint>(
            """
            SELECT
                TO_CHAR(issue_date, 'YYYY-MM')  AS Period,
                COALESCE(SUM(total_amount), 0)  AS Revenue,
                COUNT(*)                         AS InvoiceCount
            FROM invoices
            WHERE user_id    = @UserId
              AND status     = 'paid'
              AND deleted_at IS NULL
              AND (@DateFrom::date IS NULL OR issue_date >= @DateFrom::date)
              AND (@DateTo::date   IS NULL OR issue_date <= @DateTo::date)
              AND (@ClientId::uuid IS NULL OR client_id = @ClientId::uuid)
            GROUP BY TO_CHAR(issue_date, 'YYYY-MM')
            ORDER BY Period
            """,
            new { UserId = userId, DateFrom = dateFrom, DateTo = dateTo, ClientId = clientId });

        var points = byMonth.ToList();
        var totalRevenue  = points.Sum(x => x.Revenue);
        var totalInvoices = points.Sum(x => x.InvoiceCount); // long

        return new RevenueStatsResponse(totalRevenue, totalInvoices, points);
    }
}
