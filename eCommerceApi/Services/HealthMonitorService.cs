namespace eCommerceApi.Services;

using eCommerceApi.Hubs;
using Microsoft.AspNetCore.SignalR;

/// <summary>
/// Background service that periodically runs health checks and broadcasts
/// results to all connected dashboard clients so recovery is visible in real time.
/// </summary>
public class HealthMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<MetricsHub> _hubContext;
    private readonly ILogger<HealthMonitorService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public HealthMonitorService(
        IServiceProvider serviceProvider,
        IHubContext<MetricsHub> hubContext,
        ILogger<HealthMonitorService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HealthMonitorService started — broadcasting every {Interval}s.", Interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);

            try
            {
                await BroadcastHealthAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HealthMonitorService error during broadcast.");
            }
        }

        _logger.LogInformation("HealthMonitorService stopped.");
    }

    private async Task BroadcastHealthAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateAsyncScope();
        var healthService = scope.ServiceProvider.GetRequiredService<IHealthCheckService>();

        var statuses = await healthService.GetAllHealthStatusesAsync();

        foreach (var status in statuses)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveHealthStatus", status, ct);
        }

        var overallStatus = statuses.Any(s => s.Status == Models.HealthStatusEnum.Unhealthy)
            ? "Unhealthy"
            : statuses.Any(s => s.Status == Models.HealthStatusEnum.Degraded)
                ? "Degraded"
                : "Healthy";

        _logger.LogDebug("Health broadcast: {Status}", overallStatus);
    }
}
