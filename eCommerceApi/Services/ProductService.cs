using Microsoft.EntityFrameworkCore;
using eCommerceApi.Data;
using eCommerceApi.Exceptions;
using eCommerceApi.Models;
using eCommerceApi.Models.Dto;

namespace eCommerceApi.Services
{
    public class ProductService : IProductService
    {
        private readonly EcommerceContext _context;
        private readonly ITenantService _tenantService;
        private readonly ILogger<ProductService> _logger;
        private const int MaxPageSize = 1000;
        private const int MinPageSize = 1;

        public ProductService(EcommerceContext context, ITenantService tenantService, ILogger<ProductService> logger)
        {
            _context = context;
            _tenantService = tenantService;
            _logger = logger;
        }

        private Guid RequireStoreId() =>
            _tenantService.GetCurrentTenantId() ?? throw new InvalidOperationException("No store context.");

        public async Task<PaginatedResponse<Product>> GetProductsAsync(ProductQueryParameters queryParameters)
        {
            if (queryParameters.Page < 1)
                throw new BadRequestException("Page must be >= 1.");

            if (queryParameters.PageSize < MinPageSize || queryParameters.PageSize > MaxPageSize)
                throw new BadRequestException($"PageSize must be between {MinPageSize} and {MaxPageSize}.");

            var storeId = RequireStoreId();
            var query = _context.Products.AsNoTracking().Where(p => p.StoreId == storeId);

            if (!string.IsNullOrEmpty(queryParameters.ExternalId))
                query = query.Where(p => p.ExternalId == queryParameters.ExternalId);

            var totalItems = await query.CountAsync();
            var items = await query
                .Skip((queryParameters.Page - 1) * queryParameters.PageSize)
                .Take(queryParameters.PageSize)
                .ToListAsync();

            return new PaginatedResponse<Product>
            {
                TotalItems = totalItems,
                PageSize = queryParameters.PageSize,
                PageNumber = queryParameters.Page,
                TotalPages = (int)Math.Ceiling((double)totalItems / queryParameters.PageSize),
                Data = items
            };
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            var storeId = RequireStoreId();
            return await _context.Products.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId);
        }

        public async Task<Product?> GetProductByExternalIdAsync(string externalId)
        {
            var storeId = RequireStoreId();
            return await _context.Products.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ExternalId == externalId && p.StoreId == storeId);
        }

        public async Task<IEnumerable<Product>> BulkRegisterAsync(IEnumerable<RegisterProductDto> items)
        {
            var storeId = RequireStoreId();
            var dtoList = items.ToList();
            var externalIds = dtoList.Select(d => d.ExternalId).ToList();

            var existing = await _context.Products
                .Where(p => p.StoreId == storeId && externalIds.Contains(p.ExternalId))
                .Select(p => p.ExternalId)
                .ToHashSetAsync();

            var toAdd = dtoList
                .Where(d => !existing.Contains(d.ExternalId))
                .Select(d => new Product
                {
                    StoreId = storeId,
                    ExternalId = d.ExternalId,
                    Label = d.Label,
                    Price = d.Price,
                    StockUnit = d.StockUnit
                })
                .ToList();

            if (toAdd.Count > 0)
            {
                _context.Products.AddRange(toAdd);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Bulk registered {Count} products ({Skipped} duplicates skipped).",
                    toAdd.Count, dtoList.Count - toAdd.Count);
            }

            return toAdd;
        }

        public async Task<bool> UpdateProductAsync(int id, UpdateProductDto dto)
        {
            var storeId = RequireStoreId();
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId);
            if (product == null) return false;

            product.Label = dto.Label;
            product.Price = dto.Price;
            product.StockUnit = dto.StockUnit;
            product.ReorderLevel = dto.ReorderLevel;
            product.TargetStockLevel = dto.TargetStockLevel;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            var storeId = RequireStoreId();
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId);
            if (product == null) return false;

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
