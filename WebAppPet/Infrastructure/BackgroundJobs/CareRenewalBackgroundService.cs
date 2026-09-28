using WebAppPet.Application.Care.RenewCare;

namespace WebAppPet.Infrastructure.BackgroundJobs;

/// <summary>Hourly loop that charges Chombly Care memberships whose monthly cycle has ended.</summary>
public class CareRenewalBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CareRenewalBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public CareRenewalBackgroundService(IServiceScopeFactory scopeFactory, ILogger<CareRenewalBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Care renewal background service started (interval {Minutes}m)", Interval.TotalMinutes);

        // Small delay so app can finish startup / DbInitializer.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<RenewDueCareHandler>();
                var n = await handler.HandleAsync(new RenewDueCareCommand(DateTime.UtcNow), stoppingToken);
                if (n > 0)
                    _logger.LogInformation("Processed {Count} due Care membership(s)", n);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Care renewal background tick failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
