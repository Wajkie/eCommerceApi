using Microsoft.AspNetCore.Mvc;
using eCommerceApi.Models;
using eCommerceApi.Services;
using eCommerceApi.Models.Dto;

namespace eCommerceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<PaginatedResponseDto<OrderDto>>> GetOrders([FromQuery] OrderQueryParameters queryParameters)
        {
            var paginatedResult = await _orderService.GetOrdersAsync(queryParameters);
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

        [HttpGet("{id:int}")]
        public async Task<ActionResult<OrderDto>> GetOrder(int id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null)
                return NotFound(new { error = $"Order {id} not found." });

            return Ok(MapOrderToDto(order));
        }

        [HttpPost]
        public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderDto createOrderDto)
        {
            var createdOrder = await _orderService.CreateOrderAsync(createOrderDto);
            return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, MapOrderToDto(createdOrder));
        }

        [HttpPatch("{id:int}/status")]
        public async Task<ActionResult<OrderDto>> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
        {
            var updatedOrder = await _orderService.UpdateOrderStatusAsync(id, request.NewStatus, request.TrackingNumber);
            if (updatedOrder == null)
                return NotFound(new { error = $"Order with ID {id} not found." });

            return Ok(MapOrderToDto(updatedOrder));
        }

        private static OrderDto MapOrderToDto(Order order) => new()
        {
            Id = order.Id,
            OrderDate = order.OrderDate,
            CustomerId = order.CustomerId,
            Status = order.Status.ToString(),
            TrackingNumber = order.TrackingNumber,
            TotalAmount = order.TotalAmount,
            Items = order.Items.Select(MapOrderItemToDto).ToList()
        };

        private static OrderItemDto MapOrderItemToDto(OrderItem item) => new()
        {
            Id = item.Id,
            ExternalId = item.ExternalId,
            Label = item.Label,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice
        };
    }
}
