namespace eCommerceApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using eCommerceApi.Services;
using eCommerceApi.Models;

[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsService _metricsService;
    private readonly IHealthCheckService _healthCheckService;
    private readonly IAttackDetectionService _attackDetectionService;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        IMetricsService metricsService,
        IHealthCheckService healthCheckService,
        IAttackDetectionService attackDetectionService,
        ILogger<MetricsController> logger)
    {
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
        _attackDetectionService = attackDetectionService ?? throw new ArgumentNullException(nameof(attackDetectionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet("recent")]
    public async Task<ActionResult<IEnumerable<Metric>>> GetRecentMetrics([FromQuery] int count = 100)
    {
        var metrics = await _metricsService.GetRecentMetricsAsync(count);
        return Ok(metrics);
    }

    [HttpGet("endpoint/{endpoint}")]
    public async Task<ActionResult<IEnumerable<Metric>>> GetMetricsByEndpoint(string endpoint, [FromQuery] int count = 50)
    {
        var metrics = await _metricsService.GetMetricsByEndpointAsync(endpoint, count);
        return Ok(metrics);
    }

    [HttpGet("endpoint/{endpoint}/stats")]
    public async Task<ActionResult<object>> GetEndpointStats(string endpoint)
    {
        var (avg, p95, p99, errorRate) = await _metricsService.GetEndpointStatsAsync(endpoint);

        return Ok(new
        {
            endpoint,
            avgResponseTime = avg,
            p95ResponseTime = p95,
            p99ResponseTime = p99,
            errorRate
        });
    }

    [HttpGet("health")]
    public async Task<ActionResult<IEnumerable<HealthStatus>>> GetHealthStatus()
    {
        var statuses = await _healthCheckService.GetAllHealthStatusesAsync();
        return Ok(statuses);
    }

    [HttpGet("health/{serviceName}")]
    public async Task<ActionResult<HealthStatus>> GetServiceHealth(string serviceName)
    {
        var statuses = await _healthCheckService.GetAllHealthStatusesAsync();
        var status = statuses.FirstOrDefault(s => s.ServiceName == serviceName);

        if (status == null)
        {
            return NotFound($"Service '{serviceName}' not found");
        }

        return Ok(status);
    }

    [HttpGet("security/blocked-ips")]
    public async Task<ActionResult<IEnumerable<string>>> GetBlockedIps()
    {
        var blockedIps = await _attackDetectionService.GetBlockedIpsAsync();
        return Ok(blockedIps);
    }

    [HttpPost("security/block-ip/{ip}")]
    public async Task<ActionResult> BlockIp(string ip)
    {
        await _attackDetectionService.BlockIpAsync(ip);
        _logger.LogWarning($"IP {ip} manually blocked by admin");
        return Ok(new { message = $"IP {ip} has been blocked" });
    }

    [HttpPost("security/unblock-ip/{ip}")]
    public async Task<ActionResult> UnblockIp(string ip)
    {
        await _attackDetectionService.UnblockIpAsync(ip);
        _logger.LogInformation($"IP {ip} manually unblocked by admin");
        return Ok(new { message = $"IP {ip} has been unblocked" });
    }

    [HttpGet("health-check")]
    public async Task<ActionResult> HealthCheck()
    {
        try
        {
            var statuses = await _healthCheckService.GetAllHealthStatusesAsync();
            var isHealthy = statuses.All(s => s.Status == HealthStatusEnum.Healthy);

            return isHealthy ? Ok(new { status = "healthy", checks = statuses }) : StatusCode(503, new { status = "degraded", checks = statuses });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(503, new { status = "unhealthy", error = ex.Message });
        }
    }
}
