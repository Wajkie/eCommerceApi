using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using eCommerceApi.Data;
using eCommerceApi.Models;
using eCommerceApi.Services;
using eCommerceApi.Exceptions;
using eCommerceApi.Models.Dto;

namespace eCommerceApi.Tests.Features
{
    public class ShoppingCartTests
    {
        private readonly DbContextOptions<EcommerceContext> _dbContextOptions;
        private readonly Mock<IOrderService> _mockOrderService;
        private readonly Mock<ILogger<CartService>> _mockLogger;

        public ShoppingCartTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<EcommerceContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _mockOrderService = new Mock<IOrderService>();
            _mockLogger = new Mock<ILogger<CartService>>();
        }

        private static Product MakeProduct(string externalId = "sku-001", string? label = "Laptop", decimal price = 1000m, int stock = 10)
            => new Product { ExternalId = externalId, Label = label, Price = price, StockUnit = stock };

        #region Cart Creation Tests

        [Fact]
        public async Task GetOrCreateCart_WhenNoCartExists_CreatesNewCart()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);
            var customerId = Guid.NewGuid();
            var storeId = Guid.NewGuid();

            var cart = await cartService.GetOrCreateCartAsync(customerId, storeId);

            Assert.NotNull(cart);
            Assert.Equal(customerId, cart.CustomerId);
            Assert.Equal(storeId, cart.StoreId);
            Assert.Equal(CartStatus.Active, cart.Status);
            Assert.Empty(cart.Items);
        }

        [Fact]
        public async Task GetOrCreateCart_WhenActiveCartExists_ReturnExistingCart()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var customerId = Guid.NewGuid();
            var storeId = Guid.NewGuid();
            var existingCart = new Cart { CustomerId = customerId, StoreId = storeId, Status = CartStatus.Active, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
            context.Carts.Add(existingCart);
            await context.SaveChangesAsync();
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);

            var cart = await cartService.GetOrCreateCartAsync(customerId, storeId);

            Assert.NotNull(cart);
            Assert.Equal(existingCart.Id, cart.Id);
            Assert.Single(await context.Carts.ToListAsync());
        }

        [Fact]
        public async Task GetCart_WhenExpiredCartExists_MarkAsAbandoned()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var expiredCart = new Cart { Status = CartStatus.Active, ExpiresAt = DateTime.UtcNow.AddMinutes(-5) };
            context.Carts.Add(expiredCart);
            await context.SaveChangesAsync();
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);

            var cart = await cartService.GetCartAsync(expiredCart.Id);

            Assert.NotNull(cart);
            Assert.Equal(CartStatus.Abandoned, cart.Status);
        }

        #endregion

        #region Add Item Tests

        [Fact]
        public async Task AddItemToCart_WithValidItem_AddsSuccessfully()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var product = MakeProduct();
            context.Products.Add(product);
            var cart = new Cart();
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);

            var cartItem = await cartService.AddItemToCartAsync(cart.Id, "sku-001", "Laptop", 1000m, 2);

            Assert.NotNull(cartItem);
            Assert.Equal("sku-001", cartItem.ExternalId);
            Assert.Equal(2, cartItem.Quantity);
            Assert.Equal(1000m, cartItem.UnitPrice);
        }

        [Fact]
        public async Task AddItemToCart_WhenItemAlreadyExists_IncreasesQuantity()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var product = MakeProduct();
            context.Products.Add(product);
            var cart = new Cart();
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);
            await cartService.AddItemToCartAsync(cart.Id, "sku-001", "Laptop", 1000m, 2);

            var cartItem = await cartService.AddItemToCartAsync(cart.Id, "sku-001", "Laptop", 1000m, 3);

            Assert.Equal(5, cartItem.Quantity);
        }

        [Fact]
        public async Task AddItemToCart_WithInsufficientStock_ThrowsException()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var product = MakeProduct(stock: 2);
            context.Products.Add(product);
            var cart = new Cart();
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);

            await Assert.ThrowsAsync<BadRequestException>(
                () => cartService.AddItemToCartAsync(cart.Id, "sku-001", null, 1000m, 5));
        }

        [Fact]
        public async Task AddItemToCart_WithInvalidQuantity_ThrowsException()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var cart = new Cart();
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);

            await Assert.ThrowsAsync<BadRequestException>(
                () => cartService.AddItemToCartAsync(cart.Id, "sku-001", null, 10m, 0));
        }

        #endregion

        #region Update Item Tests

        [Fact]
        public async Task UpdateItemQuantity_WithValidQuantity_UpdatesSuccessfully()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var product = MakeProduct();
            context.Products.Add(product);
            var cart = new Cart();
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);
            var cartItem = await cartService.AddItemToCartAsync(cart.Id, "sku-001", "Laptop", 1000m, 2);

            var updatedItem = await cartService.UpdateItemQuantityAsync(cart.Id, cartItem.Id, 5);

            Assert.Equal(5, updatedItem.Quantity);
        }

        [Fact]
        public async Task UpdateItemQuantity_WithInsufficientStock_ThrowsException()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var product = MakeProduct(stock: 3);
            context.Products.Add(product);
            var cart = new Cart();
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);
            var cartItem = await cartService.AddItemToCartAsync(cart.Id, "sku-001", "Laptop", 1000m, 1);

            await Assert.ThrowsAsync<BadRequestException>(
                () => cartService.UpdateItemQuantityAsync(cart.Id, cartItem.Id, 5));
        }

        #endregion

        #region Remove Item Tests

        [Fact]
        public async Task RemoveItemFromCart_WithValidItem_RemovesSuccessfully()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var product = MakeProduct();
            context.Products.Add(product);
            var cart = new Cart();
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);
            var cartItem = await cartService.AddItemToCartAsync(cart.Id, "sku-001", "Laptop", 1000m, 2);

            await cartService.RemoveItemFromCartAsync(cart.Id, cartItem.Id);

            var updatedCart = await context.Carts.Include(c => c.Items).FirstAsync(c => c.Id == cart.Id);
            Assert.Empty(updatedCart.Items);
        }

        #endregion

        #region Clear Cart Tests

        [Fact]
        public async Task ClearCart_RemovesAllItems()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var product1 = MakeProduct("sku-001", "Laptop", 1000m);
            var product2 = MakeProduct("sku-002", "Phone", 500m);
            context.Products.AddRange(product1, product2);
            var cart = new Cart();
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);
            await cartService.AddItemToCartAsync(cart.Id, "sku-001", "Laptop", 1000m, 1);
            await cartService.AddItemToCartAsync(cart.Id, "sku-002", "Phone", 500m, 1);

            await cartService.ClearCartAsync(cart.Id);

            var updatedCart = await context.Carts.Include(c => c.Items).FirstAsync(c => c.Id == cart.Id);
            Assert.Empty(updatedCart.Items);
        }

        #endregion

        #region Cart Calculations Tests

        [Fact]
        public async Task Cart_CalculatesSubtotalCorrectly()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var product1 = MakeProduct("sku-001", "Laptop", 1000m);
            var product2 = MakeProduct("sku-002", "Phone", 500m);
            context.Products.AddRange(product1, product2);
            var cart = new Cart();
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);

            await cartService.AddItemToCartAsync(cart.Id, "sku-001", "Laptop", 1000m, 1);
            await cartService.AddItemToCartAsync(cart.Id, "sku-002", "Phone", 500m, 2);
            var updatedCart = await cartService.GetCartAsync(cart.Id);

            Assert.Equal(2000m, updatedCart!.Subtotal);
        }

        #endregion

        #region Checkout Tests

        [Fact]
        public async Task ConvertCartToOrder_WithValidCart_CreatesOrderSuccessfully()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var customerId = Guid.NewGuid();
            var customer = new Customer { Id = customerId, CreatedAt = DateTime.UtcNow };
            context.Customers.Add(customer);
            var product = MakeProduct();
            context.Products.Add(product);
            var cart = new Cart { CustomerId = customerId };
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);
            await cartService.AddItemToCartAsync(cart.Id, "sku-001", "Laptop", 1000m, 2);

            var createdOrder = new Order
            {
                Id = 1,
                CustomerId = customerId,
                TotalAmount = 2000m,
                Items = { new OrderItem { ExternalId = "sku-001", Label = "Laptop", Quantity = 2, UnitPrice = 1000m } }
            };
            _mockOrderService.Setup(s => s.CreateOrderAsync(It.IsAny<CreateOrderDto>())).ReturnsAsync(createdOrder);

            var order = await cartService.ConvertCartToOrderAsync(cart.Id);

            Assert.NotNull(order);
            Assert.Equal(customerId, order.CustomerId);
            Assert.Single(order.Items);
            _mockOrderService.Verify(s => s.CreateOrderAsync(It.Is<CreateOrderDto>(dto =>
                dto.CustomerId == customerId &&
                dto.Items.First().ExternalId == "sku-001")), Times.Once);
            var updatedCart = await cartService.GetCartAsync(cart.Id);
            Assert.Equal(CartStatus.Converted, updatedCart!.Status);
        }

        [Fact]
        public async Task ConvertCartToOrder_WithEmptyCart_ThrowsException()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var cart = new Cart();
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);

            await Assert.ThrowsAsync<BadRequestException>(
                () => cartService.ConvertCartToOrderAsync(cart.Id));
        }

        #endregion

        #region Cart Expiration Tests

        [Fact]
        public async Task CleanupExpiredCarts_MarksExpiredCartsAsAbandoned()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var expiredCart = new Cart { Status = CartStatus.Active, ExpiresAt = DateTime.UtcNow.AddMinutes(-5) };
            var activeCart = new Cart { Status = CartStatus.Active, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
            context.Carts.AddRange(expiredCart, activeCart);
            await context.SaveChangesAsync();
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);

            await cartService.CleanupExpiredCartsAsync();

            var carts = await context.Carts.ToListAsync();
            Assert.Equal(CartStatus.Abandoned, carts.First(c => c.Id == expiredCart.Id).Status);
            Assert.Equal(CartStatus.Active, carts.First(c => c.Id == activeCart.Id).Status);
        }

        [Fact]
        public async Task GetAbandonedCarts_ReturnsAbandonedCartsOnly()
        {
            using var context = new EcommerceContext(_dbContextOptions);
            var abandonedCart = new Cart { Status = CartStatus.Abandoned };
            var activeCart = new Cart { Status = CartStatus.Active };
            var convertedCart = new Cart { Status = CartStatus.Converted };
            context.Carts.AddRange(abandonedCart, activeCart, convertedCart);
            await context.SaveChangesAsync();
            var cartService = new CartService(context, _mockOrderService.Object, _mockLogger.Object);

            var abandonedCarts = await cartService.GetAbandonedCartsAsync();

            Assert.Single(abandonedCarts);
            Assert.Equal(CartStatus.Abandoned, abandonedCarts.First().Status);
        }

        #endregion
    }
}
