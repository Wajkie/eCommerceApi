namespace eCommerceApi.Services;

using eCommerceApi.Hubs;
using eCommerceApi.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

public class MetricsService : IMetricsService
{
    private readonly ConcurrentQueue<Metric> _metrics = new();
    private readonly IHubContext<MetricsHub> _hubContext;
    private const int MaxMetricsToKeep = 10000;

    public MetricsService(IHubContext<MetricsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task RecordMetricAsync(string endpoint, string method, int statusCode, long duration, string storeId, string? clientIp = null, string? userAgent = null)
    {
        var metric = new Metric
        {
            Endpoint = endpoint,
            Method = method,
            StatusCode = statusCode,
            Duration = duration,
            Timestamp = DateTime.UtcNow,
            StoreId = storeId,
            ClientIp = clientIp,
            UserAgent = userAgent
        };

        _metrics.Enqueue(metric);

        while (_metrics.Count > MaxMetricsToKeep)
            _metrics.TryDequeue(out _);

        // Push to all connected dashboard clients in real time
        await _hubContext.Clients.All.SendAsync("ReceiveMetric", metric);
    }

    public Task<IEnumerable<Metric>> GetRecentMetricsAsync(int count = 100)
    {
        var result = _metrics
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .ToList();

        return Task.FromResult<IEnumerable<Metric>>(result);
    }

    public Task<IEnumerable<Metric>> GetMetricsByEndpointAsync(string endpoint, int count = 50)
    {
        var result = _metrics
            .Where(m => m.Endpoint == endpoint)
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .ToList();

        return Task.FromResult<IEnumerable<Metric>>(result);
    }

    public Task<(double avgResponseTime, double p95ResponseTime, double p99ResponseTime, double errorRate)> GetEndpointStatsAsync(string endpoint)
    {
        var endpointMetrics = _metrics
            .Where(m => m.Endpoint == endpoint)
            .OrderByDescending(m => m.Timestamp)
            .Take(1000)
            .ToList();

        if (endpointMetrics.Count == 0)
            return Task.FromResult((0.0, 0.0, 0.0, 0.0));

        var durations = endpointMetrics
            .Select(m => (double)m.Duration)
            .OrderBy(d => d)
            .ToList();

        var avgResponseTime = durations.Average();
        var p95Index = (int)Math.Ceiling(durations.Count * 0.95) - 1;
        var p99Index = (int)Math.Ceiling(durations.Count * 0.99) - 1;
        var p95ResponseTime = durations[Math.Max(0, p95Index)];
        var p99ResponseTime = durations[Math.Max(0, p99Index)];
        var errorRate = (double)endpointMetrics.Count(m => m.StatusCode >= 400) / endpointMetrics.Count;

        return Task.FromResult((avgResponseTime, p95ResponseTime, p99ResponseTime, errorRate));
    }
}
