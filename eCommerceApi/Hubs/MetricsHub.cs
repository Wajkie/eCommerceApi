namespace eCommerceApi.Hubs;

using Microsoft.AspNetCore.SignalR;
using eCommerceApi.Services;
using eCommerceApi.Models;
using System.Diagnostics;

public class MetricsHub : Hub
{
    private readonly IMetricsService _metricsService;
    private readonly IHealthCheckService _healthCheckService;
    private readonly IAttackDetectionService _attackDetectionService;
    private readonly ILogger<MetricsHub> _logger;

    public MetricsHub(
        IMetricsService metricsService,
        IHealthCheckService healthCheckService,
        IAttackDetectionService attackDetectionService,
        ILogger<MetricsHub> logger)
    {
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
        _attackDetectionService = attackDetectionService ?? throw new ArgumentNullException(nameof(attackDetectionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation($"Client connected: {Context.ConnectionId}");

        // Send initial metrics data
        await SendInitialMetrics();

        await base.OnConnectedAsync();
    }

    public async Task GetMetrics()
    {
        var metrics = await _metricsService.GetRecentMetricsAsync(100);
        await Clients.Caller.SendAsync("ReceiveMetrics", metrics);
    }

    public async Task GetHealthStatus()
    {
        var statuses = await _healthCheckService.GetAllHealthStatusesAsync();
        await Clients.All.SendAsync("ReceiveHealthStatuses", statuses);
    }

    public async Task BroadcastMetric(Metric metric)
    {
        await Clients.All.SendAsync("ReceiveMetric", metric);
    }

    public async Task BroadcastHealthStatus(HealthStatus status)
    {
        await Clients.All.SendAsync("ReceiveHealthStatus", status);
    }

    public async Task BroadcastAlert(Alert alert)
    {
        await Clients.All.SendAsync("ReceiveAlert", alert);
    }

    public async Task BroadcastAttackSignal(object attackSignal)
    {
        await Clients.All.SendAsync("ReceiveAttackSignal", attackSignal);
    }

    public async Task BroadcastPerformanceMetrics(object performanceMetrics)
    {
        await Clients.All.SendAsync("ReceivePerformanceMetrics", performanceMetrics);
    }

    private async Task SendInitialMetrics()
    {
        try
        {
            var recentMetrics = await _metricsService.GetRecentMetricsAsync(50);
            await Clients.Caller.SendAsync("ReceiveMetrics", recentMetrics);

            var healthStatuses = await _healthCheckService.GetAllHealthStatusesAsync();
            await Clients.Caller.SendAsync("ReceiveHealthStatuses", healthStatuses);

            _logger.LogInformation($"Sent initial metrics to client {Context.ConnectionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending initial metrics");
        }
    }
}
