namespace eCommerceApi.Models;

public class Alert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public AlertType Type { get; set; }

    public AlertSeverity Severity { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string? AffectedEndpoint { get; set; }

    public Dictionary<string, object>? Metadata { get; set; }
}

public enum AlertType
{
    DDoS,
    HighLatency,
    HighErrorRate,
    HealthCheck,
    Anomaly
}

public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}
