namespace WebAppPet.Services;

/// <summary>Lightweight hosted loop that processes due pet care reminders every few minutes.</summary>
public class ReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public ReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reminder background service started (interval {Minutes}m)", Interval.TotalMinutes);

        // Small delay so app can finish startup / DbInitializer.
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var engine = scope.ServiceProvider.GetRequiredService<ReminderEngineService>();
                var n = await engine.ProcessDueRemindersAsync(stoppingToken);
                if (n > 0)
                    _logger.LogInformation("Processed {Count} due reminder(s)", n);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reminder background tick failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
