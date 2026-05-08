using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using eCommerceApi.Data;
using eCommerceApi.Filters;
using eCommerceApi.Models;
using eCommerceApi.Models.Dto;
using eCommerceApi.Services;

namespace eCommerceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StoresController : ControllerBase
    {
        private readonly CentralContext _centralContext;
        private readonly EcommerceContext _ecommerceContext;
        private readonly ILogger<StoresController> _logger;
        private readonly string _apiKeySecret;

        public StoresController(CentralContext centralContext, EcommerceContext ecommerceContext, ILogger<StoresController> logger, IConfiguration configuration)
        {
            _centralContext = centralContext;
            _ecommerceContext = ecommerceContext;
            _logger = logger;
            _apiKeySecret = configuration["Security:ApiKeySecret"]
                ?? throw new InvalidOperationException("Security:ApiKeySecret is not configured.");
        }

        [HttpGet]
        [RequireTotpAuth]
        public async Task<IActionResult> GetAllStores()
        {
            var stores = await _centralContext.Stores
                .Select(s => new { s.Id, s.Name, s.CreatedAt, s.TotalApiCalls, s.LastApiCallAt })
                .OrderByDescending(s => s.TotalApiCalls)
                .ToListAsync();

            return Ok(stores);
        }

        [HttpGet("{id}/Metrics")]
        public async Task<IActionResult> GetStoreMetrics(Guid id, [FromHeader(Name = "X-Api-Key")] string apiKey)
        {
            var store = await _centralContext.Stores.FindAsync(id);
            if (store == null) return NotFound("Store not found.");

            var incomingHash = ApiKeyHasher.Hash(apiKey, _apiKeySecret);
            if (!string.Equals(store.ApiKeyHash, incomingHash, StringComparison.Ordinal))
                return Unauthorized("Invalid API key for this store.");

            var totalOrders = await _ecommerceContext.Orders.Where(o => o.StoreId == id).CountAsync();
            var totalRevenue = await _ecommerceContext.Orders.Where(o => o.StoreId == id).SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            var totalCustomers = await _ecommerceContext.Customers.Where(c => c.StoreId == id).CountAsync();
            var lowStockItems = await _ecommerceContext.Products.CountAsync(p => p.StoreId == id && p.StockUnit <= p.ReorderLevel);
            var recentOrders = await _ecommerceContext.Orders
                .Where(o => o.StoreId == id)
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .Select(o => new { o.Id, o.OrderDate, o.TotalAmount, o.Status })
                .ToListAsync();

            return Ok(new
            {
                StoreName = store.Name,
                ApiUsage = new { store.TotalApiCalls, store.LastApiCallAt },
                BusinessMetrics = new
                {
                    TotalOrders = totalOrders,
                    TotalRevenue = totalRevenue,
                    TotalCustomers = totalCustomers,
                    LowStockProducts = lowStockItems,
                    RecentOrders = recentOrders
                }
            });
        }

        [HttpPost("Onboard")]
        [RequireTotpAuth]
        public async Task<IActionResult> Onboard([FromBody] OnboardStoreDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.StoreName))
                return BadRequest("Store name is required.");

            var rawKey = ApiKeyHasher.Generate();

            var store = new Store
            {
                Id = Guid.NewGuid(),
                Name = dto.StoreName,
                DbName = string.Empty,
                ApiKeyHash = ApiKeyHasher.Hash(rawKey, _apiKeySecret),
                CreatedAt = DateTime.UtcNow,
                JwksUri = dto.JwksUri,
                WebhookUrl = dto.WebhookUrl,
                WebhookSecret = dto.WebhookSecret
            };

            _centralContext.Stores.Add(store);
            await _centralContext.SaveChangesAsync();

            _logger.LogInformation("Onboarded store {StoreId} ({StoreName})", store.Id, store.Name);

            return Ok(new
            {
                Message = "Store onboarded successfully. Store your ApiKey securely — it will not be shown again.",
                StoreId = store.Id,
                StoreName = store.Name,
                ApiKey = rawKey,
                JwksUri = store.JwksUri,
                WebhookUrl = store.WebhookUrl
            });
        }
    }
}
