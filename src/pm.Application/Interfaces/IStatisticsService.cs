using pm.Application.DTOs.Statistics;

namespace pm.Application.Interfaces;

public interface IStatisticsService
{
    Task<RevenueStatsResponse> GetRevenueAsync(Guid userId, DateTime? dateFrom, DateTime? dateTo, Guid? clientId);
}
