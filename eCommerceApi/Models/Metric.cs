namespace eCommerceApi.Models;

public class Metric
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Endpoint { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public long Duration { get; set; } // milliseconds

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string StoreId { get; set; } = string.Empty;

    public string? ClientIp { get; set; }

    public string? UserAgent { get; set; }
}
