using Microsoft.EntityFrameworkCore;
using eCommerceApi.Data;
using eCommerceApi.Exceptions;
using eCommerceApi.Models;
using eCommerceApi.Models.Dto;

namespace eCommerceApi.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly EcommerceContext _context;
        private readonly ITenantService _tenantService;
        private readonly ILogger<CustomerService> _logger;
        private const int MaxPageSize = 1000;
        private const int MinPageSize = 1;

        public CustomerService(EcommerceContext context, ITenantService tenantService, ILogger<CustomerService> logger)
        {
            _context = context;
            _tenantService = tenantService;
            _logger = logger;
        }

        public async Task<IEnumerable<Customer>> GetCustomersAsync()
        {
            var storeId = _tenantService.GetCurrentTenantId() ?? throw new InvalidOperationException("No store context.");
            return await _context.Customers.AsNoTracking().Where(c => c.StoreId == storeId).ToListAsync();
        }

        public async Task<Customer?> GetCustomerByIdAsync(Guid id)
        {
            var storeId = _tenantService.GetCurrentTenantId() ?? throw new InvalidOperationException("No store context.");
            return await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.StoreId == storeId);
        }

        public async Task<Customer> CreateCustomerAsync(Customer customer)
        {
            var storeId = _tenantService.GetCurrentTenantId() ?? throw new InvalidOperationException("No store context.");
            customer.StoreId = storeId;
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task<PaginatedResponseDto<Order>> GetCustomerOrderHistoryAsync(Guid customerId, OrderHistoryQueryParameters queryParameters)
        {
            if (queryParameters.Page < 1)
                throw new BadRequestException("Page must be >= 1.");
            if (queryParameters.PageSize < MinPageSize || queryParameters.PageSize > MaxPageSize)
                throw new BadRequestException($"PageSize must be between {MinPageSize} and {MaxPageSize}.");

            var storeId = _tenantService.GetCurrentTenantId() ?? throw new InvalidOperationException("No store context.");

            var customerExists = await _context.Customers.AnyAsync(c => c.Id == customerId && c.StoreId == storeId);
            if (!customerExists)
                throw new NotFoundException($"Customer {customerId} not found.");

            var query = _context.Orders
                .AsNoTracking()
                .Where(o => o.CustomerId == customerId && o.StoreId == storeId)
                .Include(o => o.Items)
                .AsQueryable();

            if (queryParameters.StartDate.HasValue)
                query = query.Where(o => o.OrderDate >= queryParameters.StartDate.Value);

            if (queryParameters.EndDate.HasValue)
                query = query.Where(o => o.OrderDate <= queryParameters.EndDate.Value);

            var totalItems = await query.CountAsync();
            var items = await query
                .OrderByDescending(o => o.OrderDate)
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
    }
}
