using pm.Application.DTOs.Statistics;
using pm.Application.Interfaces;

namespace pm.Application.Services;

public class StatisticsService : IStatisticsService
{
    private readonly IStatisticsRepository _statisticsRepository;

    public StatisticsService(IStatisticsRepository statisticsRepository)
    {
        _statisticsRepository = statisticsRepository;
    }

    public Task<RevenueStatsResponse> GetRevenueAsync(Guid userId, DateTime? dateFrom, DateTime? dateTo, Guid? clientId)
        => _statisticsRepository.GetRevenueAsync(userId, dateFrom, dateTo, clientId);
}
