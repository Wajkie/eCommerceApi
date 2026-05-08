namespace eCommerceApi.Services;

using eCommerceApi.Models;

public interface IHealthCheckService
{
    Task<HealthStatus> CheckDatabaseHealthAsync();

    Task<HealthStatus> CheckMemoryHealthAsync();

    Task<HealthStatus> CheckCacheHealthAsync();

    Task<IEnumerable<HealthStatus>> GetAllHealthStatusesAsync();
}
