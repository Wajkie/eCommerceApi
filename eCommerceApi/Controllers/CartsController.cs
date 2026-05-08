using Microsoft.AspNetCore.Mvc;
using eCommerceApi.Models;
using eCommerceApi.Services;
using eCommerceApi.Models.Dto;

namespace eCommerceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CartsController : ControllerBase
    {
        private readonly ICartService _cartService;
        private readonly ILogger<CartsController> _logger;

        public CartsController(ICartService cartService, ILogger<CartsController> logger)
        {
            _cartService = cartService;
            _logger = logger;
        }

        [HttpGet("current")]
        public async Task<ActionResult<CartDto>> GetCurrentCart(
            [FromQuery] Guid customerId,
            [FromQuery] Guid storeId)
        {
            if (customerId == Guid.Empty || storeId == Guid.Empty)
                return BadRequest(new { error = "Valid customerId and storeId are required." });

            var cart = await _cartService.GetOrCreateCartAsync(customerId, storeId);
            return Ok(MapCartToDto(cart!));
        }

        [HttpGet("{cartId:guid}")]
        public async Task<ActionResult<CartDto>> GetCart(Guid cartId)
        {
            var cart = await _cartService.GetCartAsync(cartId);
            if (cart == null)
                return NotFound(new { error = "Cart not found." });

            return Ok(MapCartToDto(cart));
        }

        [HttpPost("{cartId:guid}/items")]
        public async Task<ActionResult<CartItemDto>> AddItemToCart(Guid cartId, [FromBody] AddCartItemRequest request)
        {
            if (request.Quantity <= 0)
                return BadRequest(new { error = "Quantity must be greater than 0." });

            var cartItem = await _cartService.AddItemToCartAsync(
                cartId, request.ExternalId, request.Label, request.UnitPrice, request.Quantity);

            return CreatedAtAction(nameof(GetCart), new { cartId = cartItem.CartId }, MapCartItemToDto(cartItem));
        }

        [HttpPut("{cartId:guid}/items/{itemId:guid}")]
        public async Task<ActionResult<CartItemDto>> UpdateItemQuantity(Guid cartId, Guid itemId, [FromBody] UpdateCartItemRequest request)
        {
            if (request.Quantity <= 0)
                return BadRequest(new { error = "Quantity must be greater than 0." });

            var cartItem = await _cartService.UpdateItemQuantityAsync(cartId, itemId, request.Quantity);
            return Ok(MapCartItemToDto(cartItem));
        }

        [HttpDelete("{cartId:guid}/items/{itemId:guid}")]
        public async Task<IActionResult> RemoveItemFromCart(Guid cartId, Guid itemId)
        {
            await _cartService.RemoveItemFromCartAsync(cartId, itemId);
            return NoContent();
        }

        [HttpDelete("{cartId:guid}/items")]
        public async Task<IActionResult> ClearCart(Guid cartId)
        {
            await _cartService.ClearCartAsync(cartId);
            return NoContent();
        }

        [HttpPost("{cartId:guid}/checkout")]
        public async Task<ActionResult<OrderDto>> CheckoutCart(Guid cartId)
        {
            var order = await _cartService.ConvertCartToOrderAsync(cartId);
            var orderDto = MapOrderToDto(order);
            return CreatedAtAction("GetOrder", "Orders", new { id = order.Id }, orderDto);
        }

        private static CartDto MapCartToDto(Cart cart) => new()
        {
            Id = cart.Id,
            CustomerId = cart.CustomerId,
            StoreId = cart.StoreId,
            Status = cart.Status,
            Subtotal = cart.Subtotal,
            Tax = cart.Tax,
            Total = cart.Total,
            ItemCount = cart.ItemCount,
            Items = cart.Items.Select(MapCartItemToDto).ToList()
        };

        private static CartItemDto MapCartItemToDto(CartItem item) => new()
        {
            Id = item.Id,
            ExternalId = item.ExternalId,
            Label = item.Label,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            TotalPrice = item.TotalPrice
        };

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
