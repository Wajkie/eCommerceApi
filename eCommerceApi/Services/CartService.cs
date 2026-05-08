using Microsoft.EntityFrameworkCore;
using eCommerceApi.Data;
using eCommerceApi.Exceptions;
using eCommerceApi.Models;
using eCommerceApi.Models.Dto;

namespace eCommerceApi.Services
{
    public class CartService : ICartService
    {
        private readonly EcommerceContext _context;
        private readonly IOrderService _orderService;
        private readonly ILogger<CartService> _logger;
        private const int CartExpirationMinutes = 15;

        public CartService(EcommerceContext context, IOrderService orderService, ILogger<CartService> logger)
        {
            _context = context;
            _orderService = orderService;
            _logger = logger;
        }

        private void ResetCartExpiration(Cart cart)
        {
            cart.LastModifiedAt = DateTime.UtcNow;
            cart.ExpiresAt = DateTime.UtcNow.AddMinutes(CartExpirationMinutes);
        }

        public async Task<Cart?> GetOrCreateCartAsync(Guid customerId, Guid storeId)
        {
            var existingCart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c =>
                    c.CustomerId == customerId &&
                    c.StoreId == storeId &&
                    c.Status == CartStatus.Active &&
                    c.ExpiresAt > DateTime.UtcNow);

            if (existingCart != null)
            {
                _logger.LogInformation("Found existing active cart {CartId} for customer {CustomerId}", existingCart.Id, customerId);
                return existingCart;
            }

            var newCart = new Cart
            {
                CustomerId = customerId,
                StoreId = storeId,
                Status = CartStatus.Active,
                CreatedAt = DateTime.UtcNow,
            };
            ResetCartExpiration(newCart);

            _context.Carts.Add(newCart);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new cart {CartId} for customer {CustomerId}", newCart.Id, customerId);
            return newCart;
        }

        public async Task<Cart?> GetCartAsync(Guid cartId)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == cartId);

            if (cart == null)
            {
                _logger.LogWarning("Cart {CartId} not found.", cartId);
                return null;
            }

            if (cart.IsExpired && cart.Status == CartStatus.Active)
            {
                cart.Status = CartStatus.Abandoned;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Cart {CartId} was expired and has been marked as abandoned.", cartId);
            }

            return cart;
        }

        public async Task<CartItem> AddItemToCartAsync(Guid cartId, string externalId, string? label, decimal unitPrice, int quantity)
        {
            if (quantity <= 0)
                throw new BadRequestException("Quantity must be greater than 0.");

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == cartId);

            if (cart == null)
                throw new NotFoundException($"Cart {cartId} not found.");
            if (cart.Status != CartStatus.Active)
                throw new BadRequestException($"Cart {cartId} is not active.");

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ExternalId == externalId && p.StoreId == cart.StoreId);
            if (product == null)
                throw new NotFoundException($"Product '{externalId}' not found.");
            if (product.StockUnit < quantity)
                throw new BadRequestException($"Insufficient stock for '{externalId}'. Available: {product.StockUnit}, Requested: {quantity}.");

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == product.Id);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
                _logger.LogInformation("Updated quantity for item {ItemId} in cart {CartId}.", existingItem.Id, cartId);
            }
            else
            {
                existingItem = new CartItem
                {
                    CartId = cartId,
                    ProductId = product.Id,
                    ExternalId = externalId,
                    Label = label ?? product.Label,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    AddedAt = DateTime.UtcNow
                };
                _context.CartItems.Add(existingItem);
                _logger.LogInformation("Added new item '{ExternalId}' to cart {CartId}.", externalId, cartId);
            }

            ResetCartExpiration(cart);
            await _context.SaveChangesAsync();
            return existingItem;
        }

        public async Task<CartItem> UpdateItemQuantityAsync(Guid cartId, Guid itemId, int newQuantity)
        {
            if (newQuantity <= 0)
                throw new BadRequestException("Quantity must be greater than 0.");

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.CartId == cartId);

            if (cartItem == null)
                throw new NotFoundException($"Item {itemId} not found in cart {cartId}.");
            if (cartItem.Cart!.Status != CartStatus.Active)
                throw new BadRequestException($"Cart {cartId} is not active.");

            var product = await _context.Products.FindAsync(cartItem.ProductId);
            if (product == null)
                throw new NotFoundException($"Product for item {itemId} not found.");
            if (product.StockUnit < newQuantity)
                throw new BadRequestException($"Insufficient stock for '{product.ExternalId}'. Available: {product.StockUnit}, Requested: {newQuantity}.");

            cartItem.Quantity = newQuantity;
            ResetCartExpiration(cartItem.Cart);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated quantity for item {ItemId} to {NewQuantity}.", itemId, newQuantity);
            return cartItem;
        }

        public async Task RemoveItemFromCartAsync(Guid cartId, Guid itemId)
        {
            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.CartId == cartId);

            if (cartItem != null)
            {
                if (cartItem.Cart!.Status != CartStatus.Active)
                    throw new BadRequestException($"Cart {cartId} is not active.");

                _context.CartItems.Remove(cartItem);
                ResetCartExpiration(cartItem.Cart);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Removed item {ItemId} from cart {CartId}.", itemId, cartId);
            }
            else
            {
                _logger.LogWarning("Attempted to remove item {ItemId} from cart {CartId}, but it was not found.", itemId, cartId);
            }
        }

        public async Task ClearCartAsync(Guid cartId)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == cartId);

            if (cart == null)
                throw new NotFoundException($"Cart {cartId} not found.");
            if (cart.Status != CartStatus.Active)
                throw new BadRequestException($"Cart {cartId} is not active.");

            _context.CartItems.RemoveRange(cart.Items);
            ResetCartExpiration(cart);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Cleared all items from cart {CartId}.", cartId);
        }

        public async Task<Order> ConvertCartToOrderAsync(Guid cartId)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == cartId);

            if (cart == null)
                throw new NotFoundException($"Cart {cartId} not found.");
            if (!cart.Items.Any())
                throw new BadRequestException("Cannot checkout an empty cart.");
            if (cart.Status != CartStatus.Active)
                throw new BadRequestException($"Cart {cartId} is not active.");

            var createOrderDto = new CreateOrderDto
            {
                CustomerId = cart.CustomerId,
                Items = cart.Items.Select(item => new CreateOrderItemDto
                {
                    ExternalId = item.ExternalId,
                    Label = item.Label,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity
                }).ToList()
            };

            var order = await _orderService.CreateOrderAsync(createOrderDto);

            cart.Status = CartStatus.Converted;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cart {CartId} successfully converted to order {OrderId}.", cartId, order.Id);
            return order;
        }

        public async Task CleanupExpiredCartsAsync()
        {
            var expiredCarts = await _context.Carts
                .Where(c => c.ExpiresAt < DateTime.UtcNow && c.Status == CartStatus.Active)
                .ToListAsync();

            if (expiredCarts.Any())
            {
                foreach (var cart in expiredCarts)
                {
                    cart.Status = CartStatus.Abandoned;
                    _logger.LogInformation("Marking cart {CartId} as abandoned due to expiration.", cart.Id);
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Cart>> GetAbandonedCartsAsync()
        {
            return await _context.Carts
                .AsNoTracking()
                .Where(c => c.Status == CartStatus.Abandoned)
                .ToListAsync();
        }
    }
}
