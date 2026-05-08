using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using eCommerceApi.Controllers;
using eCommerceApi.Models;
using eCommerceApi.Services;
using eCommerceApi.Models.Dto;

namespace eCommerceApi.Tests
{
    public class OrdersControllerTests
    {
        private readonly Mock<IOrderService> _mockOrderService;
        private readonly Mock<ILogger<OrdersController>> _mockLogger;
        private readonly OrdersController _controller;

        public OrdersControllerTests()
        {
            _mockOrderService = new Mock<IOrderService>();
            _mockLogger = new Mock<ILogger<OrdersController>>();
            _controller = new OrdersController(_mockOrderService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetOrders_ReturnsPaginatedOrders()
        {
            var queryParams = new OrderQueryParameters { Page = 1, PageSize = 10 };
            var orders = new List<Order>
            {
                new Order { Id = 1, CustomerId = Guid.NewGuid(), Status = OrderStatus.Pending, TotalAmount = 100, OrderDate = DateTime.UtcNow },
                new Order { Id = 2, CustomerId = Guid.NewGuid(), Status = OrderStatus.Shipped, TotalAmount = 200, OrderDate = DateTime.UtcNow.AddDays(-1) }
            };
            var paginatedResponse = new PaginatedResponseDto<Order>
            {
                Data = orders,
                TotalItems = 2,
                PageNumber = 1,
                PageSize = 10,
                TotalPages = 1
            };

            _mockOrderService.Setup(s => s.GetOrdersAsync(queryParams)).ReturnsAsync(paginatedResponse);

            var result = await _controller.GetOrders(queryParams);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var responseDto = Assert.IsType<PaginatedResponseDto<OrderDto>>(okResult.Value);
            Assert.Equal(2, responseDto.Data.Count());
        }

        [Fact]
        public async Task GetOrder_ReturnsOrder()
        {
            var orderId = 1;
            var order = new Order
            {
                Id = orderId,
                CustomerId = Guid.NewGuid(),
                Status = OrderStatus.Pending,
                TotalAmount = 100,
                OrderDate = DateTime.UtcNow,
                Items = new List<OrderItem>()
            };

            _mockOrderService.Setup(s => s.GetOrderByIdAsync(orderId)).ReturnsAsync(order);

            var result = await _controller.GetOrder(orderId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var orderDto = Assert.IsType<OrderDto>(okResult.Value);
            Assert.Equal(orderId, orderDto.Id);
        }

        [Fact]
        public async Task GetOrder_ReturnsNotFound_WhenOrderDoesNotExist()
        {
            _mockOrderService.Setup(s => s.GetOrderByIdAsync(999)).ReturnsAsync((Order)null);

            var result = await _controller.GetOrder(999);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateOrder_CreatesOrderSuccessfully()
        {
            var customerId = Guid.NewGuid();
            var createOrderDto = new CreateOrderDto
            {
                CustomerId = customerId,
                Items = new List<CreateOrderItemDto>
                {
                    new CreateOrderItemDto { ExternalId = "sku-001", Label = "Test Item", UnitPrice = 25m, Quantity = 2 }
                }
            };
            var createdOrder = new Order
            {
                Id = 1,
                CustomerId = customerId,
                Status = OrderStatus.Pending,
                TotalAmount = 50m,
                OrderDate = DateTime.UtcNow,
                Items = new List<OrderItem> { new OrderItem { ExternalId = "sku-001", Label = "Test Item", Quantity = 2, UnitPrice = 25m } }
            };

            _mockOrderService.Setup(s => s.CreateOrderAsync(It.IsAny<CreateOrderDto>()))
                .ReturnsAsync(createdOrder);

            var result = await _controller.CreateOrder(createOrderDto);

            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var orderDto = Assert.IsType<OrderDto>(createdResult.Value);
            Assert.Equal(OrderStatus.Pending.ToString(), orderDto.Status);
            Assert.Equal(50m, orderDto.TotalAmount);
        }

        [Fact]
        public async Task UpdateOrderStatus_UpdatesStatusSuccessfully()
        {
            var orderId = 1;
            var request = new UpdateOrderStatusRequest { NewStatus = OrderStatus.Shipped, TrackingNumber = "TRK123456" };
            var order = new Order
            {
                Id = orderId,
                Status = OrderStatus.Shipped,
                TrackingNumber = "TRK123456",
                CustomerId = Guid.NewGuid(),
                TotalAmount = 100,
                OrderDate = DateTime.UtcNow,
                Items = new List<OrderItem>()
            };

            _mockOrderService.Setup(s => s.UpdateOrderStatusAsync(orderId, request.NewStatus, request.TrackingNumber))
                .ReturnsAsync(order);

            var result = await _controller.UpdateOrderStatus(orderId, request);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var orderDto = Assert.IsType<OrderDto>(okResult.Value);
            Assert.Equal(OrderStatus.Shipped.ToString(), orderDto.Status);
            Assert.Equal("TRK123456", orderDto.TrackingNumber);
        }

        [Fact]
        public async Task UpdateOrderStatus_ReturnsNotFound_WhenOrderDoesNotExist()
        {
            var request = new UpdateOrderStatusRequest { NewStatus = OrderStatus.Shipped };
            _mockOrderService.Setup(s => s.UpdateOrderStatusAsync(999, request.NewStatus, request.TrackingNumber))
                .ReturnsAsync((Order)null);

            var result = await _controller.UpdateOrderStatus(999, request);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }
    }
}
