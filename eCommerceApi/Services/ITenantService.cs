using eCommerceApi.Models;

namespace eCommerceApi.Services
{
    public interface ITenantService
    {
        Guid? GetCurrentTenantId();
        string? GetCurrentTenantDbName();
        Store? GetCurrentStoreInfo();
    }
}