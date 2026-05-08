namespace eCommerceApi.Models
{
    public class Store
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DbName { get; set; } = string.Empty;
        public string ApiKeyHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // BYOIDP — store registers their own JWT issuer JWKS endpoint
        public string? JwksUri { get; set; }

        // Webhooks — store registers where to receive signed event payloads
        public string? WebhookUrl { get; set; }
        public string? WebhookSecret { get; set; }

        // Track API Requests
        public long TotalApiCalls { get; set; } = 0;
        public DateTime? LastApiCallAt { get; set; }
    }
}