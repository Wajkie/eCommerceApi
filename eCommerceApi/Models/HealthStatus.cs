namespace eCommerceApi.Models;

public class HealthStatus
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string ServiceName { get; set; } = string.Empty;

    public HealthStatusEnum Status { get; set; } = HealthStatusEnum.Healthy;

    public DateTime LastChecked { get; set; } = DateTime.UtcNow;

    public string Details { get; set; } = string.Empty;

    public long? ResponseTime { get; set; } // milliseconds
}

public enum HealthStatusEnum
{
    Healthy,
    Degraded,
    Unhealthy
}
