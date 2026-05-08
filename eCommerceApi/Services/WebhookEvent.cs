namespace eCommerceApi.Services;

public record WebhookEvent(Guid StoreId, string EventType, object Payload);

public class WebhookDeliveryRecord
{
    public Guid StoreId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public bool Success { get; init; }
    public int AttemptCount { get; init; }
    public DateTime DeliveredAt { get; init; }
}
