using pm.Application.DTOs.Statistics;

namespace pm.Application.Interfaces;

public interface IStatisticsRepository
{
    Task<RevenueStatsResponse> GetRevenueAsync(Guid userId, DateTime? dateFrom, DateTime? dateTo, Guid? clientId);
}
