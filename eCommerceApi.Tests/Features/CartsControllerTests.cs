using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using eCommerceApi.Controllers;
using eCommerceApi.Models;
using eCommerceApi.Models.Dto;
using eCommerceApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace eCommerceApi.Tests.Features;

public class CartsControllerTests
{
    private readonly Mock<ICartService> _mockCartService;
    private readonly Mock<ILogger<CartsController>> _mockLogger;
    private readonly CartsController _controller;

    public CartsControllerTests()
    {
        _mockCartService = new Mock<ICartService>();
        _mockLogger = new Mock<ILogger<CartsController>>();
        _controller = new CartsController(_mockCartService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetCurrentCart_WithValidIds_ReturnsCart()
    {
        var customerId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var cart = new Cart { Id = Guid.NewGuid(), CustomerId = customerId, StoreId = storeId, Items = new List<CartItem>() };

        _mockCartService.Setup(s => s.GetOrCreateCartAsync(customerId, storeId)).ReturnsAsync(cart);

        var result = await _controller.GetCurrentCart(customerId, storeId);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedCart = Assert.IsType<CartDto>(okResult.Value);
        Assert.Equal(cart.Id, returnedCart.Id);
    }

    [Fact]
    public async Task GetCurrentCart_WithEmptyIds_ReturnsBadRequest()
    {
        var result = await _controller.GetCurrentCart(Guid.Empty, Guid.Empty);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AddItemToCart_WithValidRequest_AddsItemSuccessfully()
    {
        var cartId = Guid.NewGuid();
        var request = new AddCartItemRequest { ExternalId = "sku-001", Label = "Hawaii Pizza L", UnitPrice = 12.99m, Quantity = 5 };
        var cartItem = new CartItem { Id = Guid.NewGuid(), CartId = cartId, ExternalId = "sku-001", Label = "Hawaii Pizza L", Quantity = 5, UnitPrice = 12.99m };

        _mockCartService.Setup(s => s.AddItemToCartAsync(cartId, request.ExternalId, request.Label, request.UnitPrice, request.Quantity))
            .ReturnsAsync(cartItem);

        var result = await _controller.AddItemToCart(cartId, request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returnedItem = Assert.IsType<CartItemDto>(createdResult.Value);
        Assert.Equal(5, returnedItem.Quantity);
        Assert.Equal("sku-001", returnedItem.ExternalId);
    }

    [Fact]
    public async Task AddItemToCart_WithInvalidQuantity_ReturnsBadRequest()
    {
        var result = await _controller.AddItemToCart(Guid.NewGuid(), new AddCartItemRequest { ExternalId = "sku-001", UnitPrice = 10m, Quantity = 0 });
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AddItemToCart_WithNegativeQuantity_ReturnsBadRequest()
    {
        var result = await _controller.AddItemToCart(Guid.NewGuid(), new AddCartItemRequest { ExternalId = "sku-001", UnitPrice = 10m, Quantity = -5 });
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateItemQuantity_WithValidRequest_UpdatesSuccessfully()
    {
        var cartId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new UpdateCartItemRequest { Quantity = 10 };
        var cartItem = new CartItem { Id = itemId, CartId = cartId, ExternalId = "sku-001", Quantity = 10, UnitPrice = 100 };

        _mockCartService.Setup(s => s.UpdateItemQuantityAsync(cartId, itemId, request.Quantity)).ReturnsAsync(cartItem);

        var result = await _controller.UpdateItemQuantity(cartId, itemId, request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedItem = Assert.IsType<CartItemDto>(okResult.Value);
        Assert.Equal(10, returnedItem.Quantity);
    }

    [Fact]
    public async Task UpdateItemQuantity_WithInvalidQuantity_ReturnsBadRequest()
    {
        var result = await _controller.UpdateItemQuantity(Guid.NewGuid(), Guid.NewGuid(), new UpdateCartItemRequest { Quantity = -1 });
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateItemQuantity_WithZeroQuantity_ReturnsBadRequest()
    {
        var result = await _controller.UpdateItemQuantity(Guid.NewGuid(), Guid.NewGuid(), new UpdateCartItemRequest { Quantity = 0 });
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RemoveItemFromCart_RemovesItemSuccessfully()
    {
        var cartId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        _mockCartService.Setup(s => s.RemoveItemFromCartAsync(cartId, itemId)).Returns(Task.CompletedTask);

        var result = await _controller.RemoveItemFromCart(cartId, itemId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ClearCart_ClearsCartSuccessfully()
    {
        var cartId = Guid.NewGuid();
        _mockCartService.Setup(s => s.ClearCartAsync(cartId)).Returns(Task.CompletedTask);

        var result = await _controller.ClearCart(cartId);

        Assert.IsType<NoContentResult>(result);
    }
}
