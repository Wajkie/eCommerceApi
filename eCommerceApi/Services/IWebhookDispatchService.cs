namespace eCommerceApi.Services;

public interface IWebhookDispatchService
{
    void Enqueue(WebhookEvent evt);
    IReadOnlyCollection<WebhookDeliveryRecord> GetDeliveryLog();
}
