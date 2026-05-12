using Microsoft.Extensions.Options;
using pm.Application.Interfaces;
using pm.Application.Settings;

namespace pm.API.Services;

public class OverdueInvoiceReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ReminderSettings> _settings;
    private readonly ILogger<OverdueInvoiceReminderHostedService> _logger;

    public OverdueInvoiceReminderHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<ReminderSettings> settings,
        ILogger<OverdueInvoiceReminderHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Value.Enabled)
            return;

        var initialDelay = TimeSpan.FromMinutes(Math.Max(0, _settings.Value.InitialDelayMinutes));
        if (initialDelay > TimeSpan.Zero)
            await Task.Delay(initialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessAsync(stoppingToken);

            var interval = TimeSpan.FromMinutes(Math.Max(1, _settings.Value.ScanIntervalMinutes));
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task ProcessAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var invoiceRepository = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var userIds = await invoiceRepository.GetUserIdsWithOverdueInvoicesAsync(today);

            foreach (var userId in userIds)
            {
                stoppingToken.ThrowIfCancellationRequested();
                await invoiceService.ProcessOverdueRemindersAsync(userId);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Automatic overdue invoice reminder processing failed.");
        }
    }
}
