using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using eCommerceApi.Controllers;
using eCommerceApi.Data;
using eCommerceApi.Models;
using eCommerceApi.Models.Dto;
using eCommerceApi.Services;

namespace eCommerceApi.Tests
{
    public class StoresControllerTests
    {
        private DbContextOptions<CentralContext> _dbContextOptions;
        private DbContextOptions<EcommerceContext> _ecommerceDbContextOptions;
        private readonly IConfiguration _configuration;
        private const string TestApiKeySecret = "test-server-secret-for-unit-tests-only";

        public StoresControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<CentralContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _ecommerceDbContextOptions = new DbContextOptionsBuilder<EcommerceContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:ApiKeySecret"] = TestApiKeySecret
                })
                .Build();
        }

        private StoresController CreateController(CentralContext context) =>
            new StoresController(
                context,
                new EcommerceContext(_ecommerceDbContextOptions),
                new Mock<ILogger<StoresController>>().Object,
                _configuration);

        // --- Onboard: validation ---

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public async Task Onboard_ReturnsBadRequest_WhenStoreNameIsMissingOrEmpty(string storeName)
        {
            using var context = new CentralContext(_dbContextOptions);
            var controller = CreateController(context);

            var result = await controller.Onboard(new OnboardStoreDto { StoreName = storeName });

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Store name is required.", badRequestResult.Value);
        }

        // --- Onboard: minimal (name only) ---

        [Fact]
        public async Task Onboard_WithNameOnly_ReturnsOkWithCoreFields()
        {
            using var context = new CentralContext(_dbContextOptions);
            var controller = CreateController(context);

            var result = await controller.Onboard(new OnboardStoreDto { StoreName = "My Store" });

            var ok = Assert.IsType<OkObjectResult>(result);
            var type = ok.Value!.GetType();
            Assert.Equal("My Store", type.GetProperty("StoreName")!.GetValue(ok.Value));
            Assert.NotEqual(Guid.Empty, (Guid)type.GetProperty("StoreId")!.GetValue(ok.Value)!);
            Assert.False(string.IsNullOrEmpty(type.GetProperty("ApiKey")!.GetValue(ok.Value) as string));
            Assert.Null(type.GetProperty("JwksUri")!.GetValue(ok.Value));
            Assert.Null(type.GetProperty("WebhookUrl")!.GetValue(ok.Value));
        }

        // --- Onboard: all fields ---

        [Fact]
        public async Task Onboard_WithAllFields_PersistsAndReturnsJwksAndWebhook()
        {
            using var context = new CentralContext(_dbContextOptions);
            var controller = CreateController(context);

            var dto = new OnboardStoreDto
            {
                StoreName = "Full Store",
                JwksUri = "https://tenant.auth0.com/.well-known/jwks.json",
                WebhookUrl = "https://mystore.com/webhooks",
                WebhookSecret = "super-secret-at-least-32-characters-long"
            };

            var result = await controller.Onboard(dto);

            var ok = Assert.IsType<OkObjectResult>(result);
            var type = ok.Value!.GetType();

            Assert.Equal(dto.JwksUri, type.GetProperty("JwksUri")!.GetValue(ok.Value));
            Assert.Equal(dto.WebhookUrl, type.GetProperty("WebhookUrl")!.GetValue(ok.Value));

            // Verify secret is stored in DB but NOT exposed in response
            var storeId = (Guid)type.GetProperty("StoreId")!.GetValue(ok.Value)!;
            var saved = context.Stores.Single(s => s.Id == storeId);
            Assert.Equal(dto.JwksUri, saved.JwksUri);
            Assert.Equal(dto.WebhookUrl, saved.WebhookUrl);
            Assert.Equal(dto.WebhookSecret, saved.WebhookSecret);
            Assert.Null(type.GetProperty("WebhookSecret"));
        }

        // --- Onboard: DB persistence ---

        [Fact]
        public async Task Onboard_PersistsStore_ToDatabase()
        {
            using var context = new CentralContext(_dbContextOptions);
            var controller = CreateController(context);

            var result = await controller.Onboard(new OnboardStoreDto { StoreName = "Persistent Store" });

            var ok = Assert.IsType<OkObjectResult>(result);
            var storeId = (Guid)ok.Value!.GetType().GetProperty("StoreId")!.GetValue(ok.Value)!;

            var saved = context.Stores.FirstOrDefault(s => s.Id == storeId);
            Assert.NotNull(saved);
            Assert.Equal("Persistent Store", saved!.Name);
        }

        // --- Onboard: API key is hashed in DB, raw key in response ---

        [Fact]
        public async Task Onboard_StoresHashedKey_NotRawKey()
        {
            using var context = new CentralContext(_dbContextOptions);
            var controller = CreateController(context);

            var result = await controller.Onboard(new OnboardStoreDto { StoreName = "Secure Store" });

            var ok = Assert.IsType<OkObjectResult>(result);
            var type = ok.Value!.GetType();
            var storeId = (Guid)type.GetProperty("StoreId")!.GetValue(ok.Value)!;
            var returnedRawKey = type.GetProperty("ApiKey")!.GetValue(ok.Value) as string;

            var saved = context.Stores.Single(s => s.Id == storeId);

            // Raw key must not be stored
            Assert.NotEqual(returnedRawKey, saved.ApiKeyHash);
            // Stored hash must match hashing the raw key
            var expectedHash = ApiKeyHasher.Hash(returnedRawKey!, TestApiKeySecret);
            Assert.Equal(expectedHash, saved.ApiKeyHash);
        }

        // --- Onboard: unique API keys ---

        [Fact]
        public async Task Onboard_GeneratesUniqueApiKey_ForEachStore()
        {
            using var context = new CentralContext(_dbContextOptions);
            var controller = CreateController(context);

            var r1 = await controller.Onboard(new OnboardStoreDto { StoreName = "Store 1" });
            var r2 = await controller.Onboard(new OnboardStoreDto { StoreName = "Store 2" });

            var type = ((OkObjectResult)r1).Value!.GetType();
            var key1 = type.GetProperty("ApiKey")!.GetValue(((OkObjectResult)r1).Value) as string;
            var key2 = type.GetProperty("ApiKey")!.GetValue(((OkObjectResult)r2).Value) as string;

            Assert.NotEqual(key1, key2);
            Assert.False(string.IsNullOrEmpty(key1));
            Assert.False(string.IsNullOrEmpty(key2));
        }

        // --- GetAllStores ---

        [Fact]
        public async Task GetAllStores_ReturnsAllStores()
        {
            using var context = new CentralContext(_dbContextOptions);
            context.Stores.AddRange(
                new Store { Id = Guid.NewGuid(), Name = "A", DbName = "dbA", ApiKeyHash = "h1", CreatedAt = DateTime.UtcNow },
                new Store { Id = Guid.NewGuid(), Name = "B", DbName = "dbB", ApiKeyHash = "h2", CreatedAt = DateTime.UtcNow }
            );
            context.SaveChanges();

            var controller = CreateController(context);
            var result = await controller.GetAllStores();

            var ok = Assert.IsType<OkObjectResult>(result);
            var list = ok.Value as System.Collections.IEnumerable;
            Assert.NotNull(list);
            Assert.Equal(2, list!.Cast<object>().Count());
        }
    }
}
