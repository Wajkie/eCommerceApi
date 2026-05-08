using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using eCommerceApi.Data;
using eCommerceApi.Models;
using eCommerceApi.Models.Dto;
using eCommerceApi.Services;

namespace eCommerceApi.Tests.Orders;

public class OrderIdempotencyTests
{
    private static readonly Guid StoreId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static DbContextOptions<EcommerceContext> NewOptions() =>
        new DbContextOptionsBuilder<EcommerceContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static (OrderService service, EcommerceContext context) Build(DbContextOptions<EcommerceContext> opts, Guid? storeId = null)
    {
        var context = new EcommerceContext(opts);
        var tenantSvc = new Mock<ITenantService>();
        tenantSvc.Setup(t => t.GetCurrentTenantId()).Returns(storeId ?? StoreId);
        var service = new OrderService(
            context,
            tenantSvc.Object,
            new Mock<IWebhookDispatchService>().Object,
            NullLogger<OrderService>.Instance);
        return (service, context);
    }

    private static async Task SeedAsync(EcommerceContext context, Guid? storeId = null, Guid? customerId = null)
    {
        var sid = storeId ?? StoreId;
        var cid = customerId ?? CustomerId;
        context.Customers.Add(new Customer { Id = cid, StoreId = sid });
        context.Products.Add(new Product
        {
            StoreId = sid,
            ExternalId = "sku-001",
            Label = "Widget",
            Price = 10m,
            StockUnit = 100,
            ReorderLevel = 5
        });
        await context.SaveChangesAsync();
    }

    private static CreateOrderDto MakeDto(Guid? key = null, Guid? customerId = null) => new()
    {
        CustomerId = customerId ?? CustomerId,
        IdempotencyKey = key,
        Items = new List<CreateOrderItemDto>
        {
            new() { ExternalId = "sku-001", Label = "Widget", Quantity = 1, UnitPrice = 10m }
        }
    };

    [Fact]
    public async Task SameIdempotencyKey_SecondCallReturnsExistingOrder()
    {
        var opts = NewOptions();
        var (svc, ctx) = Build(opts);
        await SeedAsync(ctx);
        var key = Guid.NewGuid();

        var first = await svc.CreateOrderAsync(MakeDto(key));
        var second = await svc.CreateOrderAsync(MakeDto(key));

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await ctx.Orders.ToListAsync());
    }

    [Fact]
    public async Task SameIdempotencyKey_SecondCallDoesNotDeductStockAgain()
    {
        var opts = NewOptions();
        var (svc, ctx) = Build(opts);
        await SeedAsync(ctx);
        var key = Guid.NewGuid();

        await svc.CreateOrderAsync(MakeDto(key));
        var stockAfterFirst = (await ctx.Products.AsNoTracking().FirstAsync(p => p.ExternalId == "sku-001")).StockUnit;

        await svc.CreateOrderAsync(MakeDto(key));
        var stockAfterSecond = (await ctx.Products.AsNoTracking().FirstAsync(p => p.ExternalId == "sku-001")).StockUnit;

        Assert.Equal(stockAfterFirst, stockAfterSecond);
    }

    [Fact]
    public async Task NoIdempotencyKey_TwoCallsCreateTwoOrders()
    {
        var opts = NewOptions();
        var (svc, ctx) = Build(opts);
        await SeedAsync(ctx);

        var first = await svc.CreateOrderAsync(MakeDto());
        var second = await svc.CreateOrderAsync(MakeDto());

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, await ctx.Orders.CountAsync());
    }

    [Fact]
    public async Task DifferentIdempotencyKeys_CreateTwoDistinctOrders()
    {
        var opts = NewOptions();
        var (svc, ctx) = Build(opts);
        await SeedAsync(ctx);

        var first = await svc.CreateOrderAsync(MakeDto(Guid.NewGuid()));
        var second = await svc.CreateOrderAsync(MakeDto(Guid.NewGuid()));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, await ctx.Orders.CountAsync());
    }

    [Fact]
    public async Task IdempotencyKey_StoredOnOrder()
    {
        var opts = NewOptions();
        var (svc, ctx) = Build(opts);
        await SeedAsync(ctx);
        var key = Guid.NewGuid();

        var order = await svc.CreateOrderAsync(MakeDto(key));

        var stored = await ctx.Orders.FindAsync(order.Id);
        Assert.Equal(key, stored!.IdempotencyKey);
    }

    [Fact]
    public async Task IdempotencyKeyIsolatedByStore_DifferentStoreCanReuseKey()
    {
        var opts = NewOptions();
        var storeA = Guid.NewGuid();
        var storeB = Guid.NewGuid();
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var sharedKey = Guid.NewGuid();

        // Seed both stores with their own customers and products
        using (var ctx = new EcommerceContext(opts))
        {
            ctx.Customers.Add(new Customer { Id = customerA, StoreId = storeA });
            ctx.Customers.Add(new Customer { Id = customerB, StoreId = storeB });
            ctx.Products.Add(new Product { StoreId = storeA, ExternalId = "sku-001", Label = "W", Price = 10m, StockUnit = 50, ReorderLevel = 2 });
            ctx.Products.Add(new Product { StoreId = storeB, ExternalId = "sku-001", Label = "W", Price = 10m, StockUnit = 50, ReorderLevel = 2 });
            await ctx.SaveChangesAsync();
        }

        // Each store places an order with the same idempotency key
        int orderAId, orderBId;
        using (var ctx = new EcommerceContext(opts))
        {
            var (svcA, _) = Build(opts, storeA);
            var orderA = await svcA.CreateOrderAsync(MakeDto(sharedKey, customerA));
            orderAId = orderA.Id;
        }
        using (var ctx = new EcommerceContext(opts))
        {
            var (svcB, _) = Build(opts, storeB);
            var orderB = await svcB.CreateOrderAsync(MakeDto(sharedKey, customerB));
            orderBId = orderB.Id;
        }

        Assert.NotEqual(orderAId, orderBId);

        using var verify = new EcommerceContext(opts);
        Assert.Equal(2, await verify.Orders.CountAsync());
    }
}
