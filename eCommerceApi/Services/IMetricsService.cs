namespace eCommerceApi.Services;

using eCommerceApi.Models;

public interface IMetricsService
{
    Task RecordMetricAsync(string endpoint, string method, int statusCode, long duration, string storeId, string? clientIp = null, string? userAgent = null);

    Task<IEnumerable<Metric>> GetRecentMetricsAsync(int count = 100);

    Task<IEnumerable<Metric>> GetMetricsByEndpointAsync(string endpoint, int count = 50);

    Task<(double avgResponseTime, double p95ResponseTime, double p99ResponseTime, double errorRate)> GetEndpointStatsAsync(string endpoint);
}
