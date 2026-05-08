using eCommerceApi.Data;
using eCommerceApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace eCommerceApi.Services
{
    public class TenantService : ITenantService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly CentralContext _centralContext;
        private readonly IMemoryCache _cache;

        public TenantService(IHttpContextAccessor httpContextAccessor, CentralContext centralContext, IMemoryCache cache)
        {
            _httpContextAccessor = httpContextAccessor;
            _centralContext = centralContext;
            _cache = cache;
        }

        public Guid? GetCurrentTenantId()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context != null && context.Request.Headers.TryGetValue("X-Store-Id", out var storeIdStr))
            {
                if (Guid.TryParse(storeIdStr, out var storeId))
                {
                    return storeId;
                }
            }
            return null;
        }

        public Store? GetCurrentStoreInfo()
        {
            var storeId = GetCurrentTenantId();
            if (!storeId.HasValue) return null;

            var cacheKey = $"StoreCache_{storeId.Value}";
            if (!_cache.TryGetValue(cacheKey, out Store? store))
            {
                store = _centralContext.Stores.FirstOrDefault(s => s.Id == storeId.Value);
                if (store != null)
                {
                    _cache.Set(cacheKey, store, TimeSpan.FromMinutes(5));
                }
            }
            return store;
        }

        public string? GetCurrentTenantDbName()
        {
            return GetCurrentStoreInfo()?.DbName;
        }
    }
}