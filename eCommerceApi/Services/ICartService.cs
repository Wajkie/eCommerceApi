using eCommerceApi.Models;

namespace eCommerceApi.Services
{
    public interface ICartService
    {
        Task<Cart?> GetOrCreateCartAsync(Guid customerId, Guid storeId);
        Task<Cart?> GetCartAsync(Guid cartId);
        Task<CartItem> AddItemToCartAsync(Guid cartId, string externalId, string? label, decimal unitPrice, int quantity);
        Task<CartItem> UpdateItemQuantityAsync(Guid cartId, Guid itemId, int newQuantity);
        Task RemoveItemFromCartAsync(Guid cartId, Guid itemId);
        Task ClearCartAsync(Guid cartId);
        Task<Order> ConvertCartToOrderAsync(Guid cartId);
        Task CleanupExpiredCartsAsync();
        Task<IEnumerable<Cart>> GetAbandonedCartsAsync();
    }
}
