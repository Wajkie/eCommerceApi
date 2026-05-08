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
    public class CustomersControllerTests
    {
        private readonly Mock<ICustomerService> _mockCustomerService;
        private readonly Mock<ILogger<CustomersController>> _mockLogger;
        private readonly CustomersController _controller;

        public CustomersControllerTests()
        {
            _mockCustomerService = new Mock<ICustomerService>();
            _mockLogger = new Mock<ILogger<CustomersController>>();
            _controller = new CustomersController(_mockCustomerService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetCustomers_ReturnsAllCustomers()
        {
            var customers = new List<Customer>
            {
                new Customer { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow.AddDays(-1) },
                new Customer { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow }
            };

            _mockCustomerService.Setup(s => s.GetCustomersAsync()).ReturnsAsync(customers);

            var result = await _controller.GetCustomers();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedCustomers = Assert.IsAssignableFrom<IEnumerable<CustomerDto>>(okResult.Value);
            Assert.Equal(2, returnedCustomers.Count());
        }

        [Fact]
        public async Task GetCustomer_ReturnsCustomer()
        {
            var customerId = Guid.NewGuid();
            var customer = new Customer { Id = customerId, CreatedAt = DateTime.UtcNow };
            _mockCustomerService.Setup(s => s.GetCustomerByIdAsync(customerId)).ReturnsAsync(customer);

            var result = await _controller.GetCustomer(customerId);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var customerDto = Assert.IsType<CustomerDto>(okResult.Value);
            Assert.Equal(customerId, customerDto.Id);
        }

        [Fact]
        public async Task GetCustomer_ReturnsNotFound_WhenCustomerDoesNotExist()
        {
            var customerId = Guid.NewGuid();
            _mockCustomerService.Setup(s => s.GetCustomerByIdAsync(customerId)).ReturnsAsync((Customer)null);

            var result = await _controller.GetCustomer(customerId);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task CreateCustomer_CreatesCustomer()
        {
            var customerId = Guid.NewGuid();
            var createCustomerDto = new CreateCustomerDto { Id = customerId };
            var createdCustomer = new Customer { Id = customerId, CreatedAt = DateTime.UtcNow };

            _mockCustomerService.Setup(s => s.CreateCustomerAsync(It.IsAny<Customer>()))
                .ReturnsAsync(createdCustomer);

            var result = await _controller.CreateCustomer(createCustomerDto);

            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var customerDto = Assert.IsType<CustomerDto>(createdAtActionResult.Value);
            Assert.Equal(customerId, customerDto.Id);
        }

        [Fact]
        public async Task GetCustomerOrderHistory_ReturnsPaginatedOrders()
        {
            var customerId = Guid.NewGuid();
            var queryParams = new OrderHistoryQueryParameters { Page = 1, PageSize = 2 };
            var orders = new List<Order>
            {
                new Order { CustomerId = customerId, OrderDate = DateTime.UtcNow.AddDays(-2), TotalAmount = 100 },
                new Order { CustomerId = customerId, OrderDate = DateTime.UtcNow.AddDays(-1), TotalAmount = 200 }
            };
            var paginatedResponse = new PaginatedResponseDto<Order>
            {
                TotalItems = 3,
                PageSize = 2,
                PageNumber = 1,
                TotalPages = 2,
                Data = orders
            };

            _mockCustomerService.Setup(s => s.GetCustomerOrderHistoryAsync(customerId, queryParams))
                .ReturnsAsync(paginatedResponse);

            var result = await _controller.GetCustomerOrderHistory(customerId, queryParams);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var responseDto = Assert.IsType<PaginatedResponseDto<OrderDto>>(okResult.Value);
            Assert.Equal(3, responseDto.TotalItems);
            Assert.Equal(2, responseDto.Data.Count());
        }
    }
}
