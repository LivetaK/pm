namespace pm.Application.DTOs.Statistics;

public record RevenueDataPoint(string Period, decimal Revenue, long InvoiceCount);

public record RevenueStatsResponse(
    decimal TotalRevenue,
    long TotalInvoices,
    List<RevenueDataPoint> ByMonth
);
