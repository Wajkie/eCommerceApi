using System.Security.Cryptography;
using System.Text;
using eCommerceApi.Services;
using Microsoft.Extensions.Configuration;

namespace eCommerceApi.Middleware
{
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantMiddleware> _logger;
        private readonly string _apiKeySecret;

        private static readonly string[] TenantPaths =
            { "/api/Products", "/api/Orders", "/api/Customers", "/api/Carts" };

        public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger, IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _apiKeySecret = configuration["Security:ApiKeySecret"]
                ?? throw new InvalidOperationException("Security:ApiKeySecret is not configured.");
        }

        public async Task InvokeAsync(HttpContext context, ITenantService tenantService, IMetricsQueue metricsQueue)
        {
            var path = context.Request.Path;

            var isTenantPath = TenantPaths.Any(p => path.StartsWithSegments(p));
            if (!isTenantPath)
            {
                await _next(context);
                return;
            }

            // Require X-Store-Id
            var storeId = tenantService.GetCurrentTenantId();
            if (!storeId.HasValue)
            {
                _logger.LogWarning("Request to {Path} rejected: missing X-Store-Id from {Ip}", path, context.Connection.RemoteIpAddress);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid X-Store-Id header." });
                return;
            }

            // Require X-Api-Key on every request
            if (!context.Request.Headers.TryGetValue("X-Api-Key", out var rawKey) || string.IsNullOrEmpty(rawKey))
            {
                _logger.LogWarning("Request to {Path} rejected: missing X-Api-Key for store {StoreId}", path, storeId);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Missing X-Api-Key header." });
                return;
            }

            // Load store (hits in-memory cache in TenantService, DB only on first miss)
            var store = tenantService.GetCurrentStoreInfo();
            if (store == null)
            {
                _logger.LogWarning("Request to {Path} rejected: store {StoreId} not found", path, storeId);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Store not found." });
                return;
            }

            // Hash the incoming key and compare — timing-safe, constant time
            var incomingHash = ApiKeyHasher.Hash(rawKey!, _apiKeySecret);
            if (!TimingSafeEquals(store.ApiKeyHash, incomingHash))
            {
                _logger.LogWarning("Request to {Path} rejected: invalid API key for store {StoreId} from {Ip}", path, storeId, context.Connection.RemoteIpAddress);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid API key." });
                return;
            }

            await metricsQueue.EnqueueAsync(storeId.Value, context.RequestAborted);
            await _next(context);
        }

        private static bool TimingSafeEquals(string a, string b)
        {
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);
            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
    }
}
