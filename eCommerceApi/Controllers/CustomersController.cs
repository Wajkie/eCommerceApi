using Microsoft.AspNetCore.Mvc;
using eCommerceApi.Models;
using eCommerceApi.Services;
using eCommerceApi.Models.Dto;

namespace eCommerceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(ICustomerService customerService, ILogger<CustomersController> logger)
        {
            _customerService = customerService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerDto>>> GetCustomers()
        {
            var customers = await _customerService.GetCustomersAsync();
            return Ok(customers.Select(c => new CustomerDto { Id = c.Id, CreatedAt = c.CreatedAt }));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<CustomerDto>> GetCustomer(Guid id)
        {
            var customer = await _customerService.GetCustomerByIdAsync(id);
            if (customer is null)
                return NotFound(new { error = $"Customer {id} not found." });

            return Ok(new CustomerDto { Id = customer.Id, CreatedAt = customer.CreatedAt });
        }

        [HttpPost]
        public async Task<ActionResult<CustomerDto>> CreateCustomer([FromBody] CreateCustomerDto createCustomerDto)
        {
            var customer = new Customer { Id = createCustomerDto.Id, CreatedAt = DateTime.UtcNow };
            var createdCustomer = await _customerService.CreateCustomerAsync(customer);
            var customerDto = new CustomerDto { Id = createdCustomer.Id, CreatedAt = createdCustomer.CreatedAt };
            return CreatedAtAction(nameof(GetCustomer), new { id = createdCustomer.Id }, customerDto);
        }

        [HttpGet("{id:guid}/orders")]
        public async Task<ActionResult<PaginatedResponseDto<OrderDto>>> GetCustomerOrderHistory(Guid id, [FromQuery] OrderHistoryQueryParameters queryParameters)
        {
            var paginatedResult = await _customerService.GetCustomerOrderHistoryAsync(id, queryParameters);
            var paginatedDto = new PaginatedResponseDto<OrderDto>
            {
                TotalItems = paginatedResult.TotalItems,
                PageSize = paginatedResult.PageSize,
                PageNumber = paginatedResult.PageNumber,
                TotalPages = paginatedResult.TotalPages,
                Data = paginatedResult.Data.Select(MapOrderToDto)
            };
            return Ok(paginatedDto);
        }

        private static OrderDto MapOrderToDto(Order order) => new()
        {
            Id = order.Id,
            OrderDate = order.OrderDate,
            CustomerId = order.CustomerId,
            Status = order.Status.ToString(),
            TrackingNumber = order.TrackingNumber,
            TotalAmount = order.TotalAmount,
            Items = order.Items.Select(item => new OrderItemDto
            {
                Id = item.Id,
                ExternalId = item.ExternalId,
                Label = item.Label,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList()
        };
    }
}
