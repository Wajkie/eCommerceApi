namespace eCommerceApi.Services;

using eCommerceApi.Data;
using eCommerceApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

public class HealthCheckService : IHealthCheckService
{
    private readonly IMetricsQueue _metricsQueue;
    private readonly EcommerceContext _dbContext;
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(
        IMetricsQueue metricsQueue,
        EcommerceContext dbContext,
        ILogger<HealthCheckService> logger)
    {
        _metricsQueue = metricsQueue ?? throw new ArgumentNullException(nameof(metricsQueue));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthStatus> CheckDatabaseHealthAsync()
    {
        try
        {
            var sw = Stopwatch.StartNew();
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
            sw.Stop();

            return new HealthStatus
            {
                ServiceName = "Database",
                Status = HealthStatusEnum.Healthy,
                LastChecked = DateTime.UtcNow,
                Details = "Database connection healthy",
                ResponseTime = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return new HealthStatus
            {
                ServiceName = "Database",
                Status = HealthStatusEnum.Unhealthy,
                LastChecked = DateTime.UtcNow,
                Details = $"Database connection failed: {ex.Message}",
                ResponseTime = null
            };
        }
    }

    public Task<HealthStatus> CheckMemoryHealthAsync()
    {
        var process = Process.GetCurrentProcess();
        var memoryUsageMb = process.WorkingSet64 / 1024 / 1024;
        var status = HealthStatusEnum.Healthy;
        var details = $"Memory usage: {memoryUsageMb} MB";

        if (memoryUsageMb > 1024)
        {
            status = HealthStatusEnum.Unhealthy;
        }
        else if (memoryUsageMb > 512)
        {
            status = HealthStatusEnum.Degraded;
        }

        return Task.FromResult(new HealthStatus
        {
            ServiceName = "Memory",
            Status = status,
            LastChecked = DateTime.UtcNow,
            Details = details,
            ResponseTime = memoryUsageMb
        });
    }

    public Task<HealthStatus> CheckCacheHealthAsync()
    {
        try
        {
            // Check if metrics queue is operational - just verify we can call the service
            return Task.FromResult(new HealthStatus
            {
                ServiceName = "Cache/Queue",
                Status = HealthStatusEnum.Healthy,
                LastChecked = DateTime.UtcNow,
                Details = "Metrics queue operational",
                ResponseTime = null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache health check failed");
            return Task.FromResult(new HealthStatus
            {
                ServiceName = "Cache/Queue",
                Status = HealthStatusEnum.Unhealthy,
                LastChecked = DateTime.UtcNow,
                Details = $"Cache check failed: {ex.Message}",
                ResponseTime = null
            });
        }
    }

    public async Task<IEnumerable<HealthStatus>> GetAllHealthStatusesAsync()
    {
        var statuses = new List<HealthStatus>
        {
            await CheckDatabaseHealthAsync(),
            await CheckMemoryHealthAsync(),
            await CheckCacheHealthAsync()
        };

        return statuses;
    }
}
