namespace eCommerceApi.Models.Dto;

public class OnboardStoreDto
{
    public string StoreName { get; set; } = string.Empty;

    /// <summary>JWKS endpoint of the store's JWT issuer (e.g. https://tenant.auth0.com/.well-known/jwks.json)</summary>
    public string? JwksUri { get; set; }

    /// <summary>HTTPS URL where signed webhook payloads will be POSTed.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Secret used to sign webhook payloads (HMAC-SHA256). Min 32 chars recommended.</summary>
    public string? WebhookSecret { get; set; }
}
