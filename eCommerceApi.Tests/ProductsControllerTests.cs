using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using eCommerceApi.Controllers;
using eCommerceApi.Models;
using eCommerceApi.Services;
using eCommerceApi.Models.Dto;
using System.Linq;
using System.Threading.Tasks;

namespace eCommerceApi.Tests
{
    public class ProductsControllerTests
    {
        private readonly Mock<IProductService> _mockProductService;
        private readonly Mock<ILogger<ProductsController>> _mockLogger;
        private readonly ProductsController _controller;

        public ProductsControllerTests()
        {
            _mockProductService = new Mock<IProductService>();
            _mockLogger = new Mock<ILogger<ProductsController>>();
            _controller = new ProductsController(_mockProductService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetProducts_ReturnsPaginatedList()
        {
            var queryParameters = new ProductQueryParameters { Page = 1, PageSize = 20 };
            var products = new List<Product>
            {
                new Product { Id = 1, ExternalId = "sku-001", Label = "Product 1" },
                new Product { Id = 2, ExternalId = "sku-002", Label = "Product 2" }
            };
            var paginatedResponse = new PaginatedResponse<Product>
            {
                Data = products,
                TotalItems = products.Count,
                PageNumber = 1,
                PageSize = 20,
                TotalPages = 1
            };

            _mockProductService.Setup(s => s.GetProductsAsync(queryParameters)).ReturnsAsync(paginatedResponse);

            var result = await _controller.GetProducts(queryParameters);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var responseDto = Assert.IsType<PaginatedResponseDto<ProductDto>>(okResult.Value);
            Assert.Equal(2, responseDto.Data.Count());
        }

        [Fact]
        public async Task BulkRegister_CreatesProducts()
        {
            var items = new List<RegisterProductDto>
            {
                new RegisterProductDto { ExternalId = "sku-001", Label = "Hawaii Pizza L", Price = 12.99m, StockUnit = 50 }
            };
            var created = new List<Product>
            {
                new Product { Id = 1, ExternalId = "sku-001", Label = "Hawaii Pizza L", Price = 12.99m, StockUnit = 50 }
            };

            _mockProductService.Setup(s => s.BulkRegisterAsync(It.IsAny<IEnumerable<RegisterProductDto>>()))
                .ReturnsAsync(created);

            var result = await _controller.BulkRegister(items);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dtos = Assert.IsAssignableFrom<IEnumerable<ProductDto>>(okResult.Value);
            Assert.Single(dtos);
            Assert.Equal("sku-001", dtos.First().ExternalId);
        }

        [Fact]
        public async Task BulkRegister_WithEmptyList_ReturnsBadRequest()
        {
            var result = await _controller.BulkRegister(new List<RegisterProductDto>());

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetProductById_ReturnsProduct()
        {
            var product = new Product { Id = 1, ExternalId = "sku-001", Label = "Test", Price = 100 };
            _mockProductService.Setup(s => s.GetProductByIdAsync(1)).ReturnsAsync(product);

            var result = await _controller.GetProduct(1);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var productDto = Assert.IsType<ProductDto>(okResult.Value);
            Assert.Equal(1, productDto.Id);
            Assert.Equal("sku-001", productDto.ExternalId);
        }

        [Fact]
        public async Task GetProductById_ReturnsNotFound_WhenProductDoesNotExist()
        {
            _mockProductService.Setup(s => s.GetProductByIdAsync(999)).ReturnsAsync((Product)null);

            var result = await _controller.GetProduct(999);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task UpdateProduct_UpdatesProductSuccessfully()
        {
            var updateDto = new UpdateProductDto { Label = "Updated", Price = 150m, StockUnit = 50 };
            _mockProductService.Setup(s => s.UpdateProductAsync(1, It.IsAny<UpdateProductDto>())).ReturnsAsync(true);

            var result = await _controller.UpdateProduct(1, updateDto);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteProduct_DeletesProductSuccessfully()
        {
            _mockProductService.Setup(s => s.DeleteProductAsync(1)).ReturnsAsync(true);

            var result = await _controller.DeleteProduct(1);

            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteProduct_ReturnsNotFound_WhenProductDoesNotExist()
        {
            _mockProductService.Setup(s => s.DeleteProductAsync(999)).ReturnsAsync(false);

            var result = await _controller.DeleteProduct(999);

            Assert.IsType<NotFoundResult>(result);
        }
    }
}
