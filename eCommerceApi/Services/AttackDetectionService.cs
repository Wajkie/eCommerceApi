namespace eCommerceApi.Services;

using eCommerceApi.Models;
using System.Collections.Concurrent;

public class AttackDetectionService : IAttackDetectionService
{
    private readonly ConcurrentDictionary<string, (int count, DateTime timestamp)> _requestCounts = new();
    private readonly ConcurrentDictionary<string, DateTime> _blockedIps = new();
    private readonly ConcurrentQueue<int> _requestsPerSecond = new();

    private const int DDoSThreshold = 100; // requests per IP per minute
    private const int HighRequestRateThreshold = 1000; // requests per second across all IPs
    private const int BlockDurationMinutes = 15;

    public Task<bool> CheckForDDoSAsync(string clientIp)
    {
        // Check if IP is already blocked
        if (_blockedIps.TryGetValue(clientIp, out var blockedTime))
        {
            if (DateTime.UtcNow - blockedTime > TimeSpan.FromMinutes(BlockDurationMinutes))
            {
                _blockedIps.TryRemove(clientIp, out _);
            }
            else
            {
                return Task.FromResult(true); // Still blocked
            }
        }

        // Check request frequency for this IP
        if (_requestCounts.TryGetValue(clientIp, out var info))
        {
            var timeDiff = DateTime.UtcNow - info.timestamp;
            if (timeDiff.TotalMinutes < 1)
            {
                if (info.count > DDoSThreshold)
                {
                    return Task.FromResult(true); // Potential DDoS
                }
            }
            else
            {
                // Reset if outside window
                _requestCounts[clientIp] = (1, DateTime.UtcNow);
            }
        }

        return Task.FromResult(false);
    }

    public Task<Alert?> CheckForAnomaliesAsync()
    {
        // Check if overall request rate is unusually high
        var now = DateTime.UtcNow;
        var recentRequests = _requestsPerSecond
            .Where(x => true) // Would need timestamp tracking for real implementation
            .Count();

        if (recentRequests > HighRequestRateThreshold)
        {
            return Task.FromResult<Alert?>(new Alert
            {
                Type = AlertType.DDoS,
                Severity = AlertSeverity.Critical,
                Message = $"High request rate detected: {recentRequests} requests/second",
                Timestamp = now,
                Metadata = new Dictionary<string, object>
                {
                    { "requestRate", recentRequests },
                    { "threshold", HighRequestRateThreshold }
                }
            });
        }

        return Task.FromResult<Alert?>(null);
    }

    public void RecordRequest(string clientIp)
    {
        _requestsPerSecond.Enqueue(1);

        _requestCounts.AddOrUpdate(clientIp,
            (1, DateTime.UtcNow),
            (key, existing) =>
            {
                var timeDiff = DateTime.UtcNow - existing.timestamp;
                if (timeDiff.TotalMinutes < 1)
                {
                    return (existing.count + 1, existing.timestamp);
                }
                return (1, DateTime.UtcNow);
            });

        // Keep queue size manageable
        while (_requestsPerSecond.Count > 10000)
        {
            _requestsPerSecond.TryDequeue(out _);
        }
    }

    public Task BlockIpAsync(string clientIp)
    {
        _blockedIps[clientIp] = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task UnblockIpAsync(string clientIp)
    {
        _blockedIps.TryRemove(clientIp, out _);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> GetBlockedIpsAsync()
    {
        return Task.FromResult<IEnumerable<string>>(_blockedIps.Keys);
    }
}
