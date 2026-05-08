using Microsoft.EntityFrameworkCore;
using eCommerceApi.Data;
using eCommerceApi.Exceptions;
using eCommerceApi.Models;
using eCommerceApi.Models.Dto;

namespace eCommerceApi.Services
{
    public class OrderService : IOrderService
    {
        private readonly EcommerceContext _context;
        private readonly ITenantService _tenantService;
        private readonly IWebhookDispatchService _webhooks;
        private readonly ILogger<OrderService> _logger;
        private const int MaxPageSize = 1000;
        private const int MinPageSize = 1;

        public OrderService(EcommerceContext context, ITenantService tenantService, IWebhookDispatchService webhooks, ILogger<OrderService> logger)
        {
            _context = context;
            _tenantService = tenantService;
            _webhooks = webhooks;
            _logger = logger;
        }

        public async Task<PaginatedResponseDto<Order>> GetOrdersAsync(OrderQueryParameters queryParameters)
        {
            if (queryParameters.Page < 1)
                throw new BadRequestException("Page must be >= 1.");
            if (queryParameters.PageSize < MinPageSize || queryParameters.PageSize > MaxPageSize)
                throw new BadRequestException($"PageSize must be between {MinPageSize} and {MaxPageSize}.");

            var storeId = _tenantService.GetCurrentTenantId() ?? throw new InvalidOperationException("No store context.");
            var query = _context.Orders
                .AsNoTracking()
                .Where(o => o.StoreId == storeId)
                .Include(o => o.Items)
                .OrderByDescending(o => o.OrderDate);

            var totalItems = await query.CountAsync();
            var items = await query
                .Skip((queryParameters.Page - 1) * queryParameters.PageSize)
                .Take(queryParameters.PageSize)
                .ToListAsync();

            return new PaginatedResponseDto<Order>
            {
                TotalItems = totalItems,
                PageSize = queryParameters.PageSize,
                PageNumber = queryParameters.Page,
                TotalPages = (int)Math.Ceiling((double)totalItems / queryParameters.PageSize),
                Data = items
            };
        }

        public async Task<Order?> GetOrderByIdAsync(int id)
        {
            var storeId = _tenantService.GetCurrentTenantId() ?? throw new InvalidOperationException("No store context.");
            return await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id && o.StoreId == storeId);
        }

        public async Task<Order> CreateOrderAsync(CreateOrderDto createOrderDto)
        {
            var storeId = _tenantService.GetCurrentTenantId() ?? throw new InvalidOperationException("No store context.");

            if (createOrderDto.IdempotencyKey.HasValue)
            {
                var existing = await _context.Orders
                    .AsNoTracking()
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.StoreId == storeId && o.IdempotencyKey == createOrderDto.IdempotencyKey);
                if (existing != null)
                {
                    _logger.LogInformation("Idempotent duplicate for key {Key} — returning existing order {OrderId}.", createOrderDto.IdempotencyKey, existing.Id);
                    return existing;
                }
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var customer = await _context.Customers.FindAsync(createOrderDto.CustomerId);
                if (customer == null)
                    throw new NotFoundException($"Customer {createOrderDto.CustomerId} not found.");

                var externalIds = createOrderDto.Items.Select(i => i.ExternalId).ToList();
                var products = await _context.Products
                    .Where(p => p.StoreId == storeId && externalIds.Contains(p.ExternalId))
                    .ToDictionaryAsync(p => p.ExternalId);

                var orderItems = new List<OrderItem>();
                decimal totalAmount = 0;

                var lowStockProducts = new List<Product>();

                foreach (var itemDto in createOrderDto.Items)
                {
                    if (!products.TryGetValue(itemDto.ExternalId, out var product))
                        throw new NotFoundException($"Product '{itemDto.ExternalId}' not found.");
                    if (product.StockUnit < itemDto.Quantity)
                        throw new BadRequestException($"Insufficient stock for '{itemDto.ExternalId}'. Available: {product.StockUnit}, Requested: {itemDto.Quantity}.");

                    product.StockUnit -= itemDto.Quantity;

                    if (product.StockUnit <= product.ReorderLevel)
                        lowStockProducts.Add(product);

                    orderItems.Add(new OrderItem
                    {
                        ProductId = product.Id,
                        ExternalId = itemDto.ExternalId,
                        Label = itemDto.Label,
                        Quantity = itemDto.Quantity,
                        UnitPrice = itemDto.UnitPrice
                    });
                    totalAmount += itemDto.UnitPrice * itemDto.Quantity;
                }

                var order = new Order
                {
                    StoreId = storeId,
                    IdempotencyKey = createOrderDto.IdempotencyKey,
                    CustomerId = createOrderDto.CustomerId,
                    OrderDate = DateTime.UtcNow,
                    Status = OrderStatus.Pending,
                    TotalAmount = totalAmount,
                    Items = orderItems
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Created order {OrderId} for customer {CustomerId}.", order.Id, order.CustomerId);

                _webhooks.Enqueue(new WebhookEvent(storeId, "order.created", new
                {
                    order.Id,
                    order.CustomerId,
                    order.TotalAmount,
                    order.Status,
                    order.OrderDate
                }));

                foreach (var p in lowStockProducts)
                {
                    _webhooks.Enqueue(new WebhookEvent(storeId, "product.low_stock", new
                    {
                        p.Id,
                        p.ExternalId,
                        p.StockUnit,
                        p.ReorderLevel
                    }));
                }

                return order;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating order for customer {CustomerId}.", createOrderDto.CustomerId);
                throw;
            }
        }

        public async Task<Order?> UpdateOrderStatusAsync(int id, OrderStatus newStatus, string? trackingNumber)
        {
            var storeId = _tenantService.GetCurrentTenantId() ?? throw new InvalidOperationException("No store context.");
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.StoreId == storeId);
            if (order == null) return null;

            order.Status = newStatus;
            if (!string.IsNullOrEmpty(trackingNumber))
                order.TrackingNumber = trackingNumber;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated status for order {OrderId} to {NewStatus}.", id, newStatus);
            return order;
        }
    }
}
