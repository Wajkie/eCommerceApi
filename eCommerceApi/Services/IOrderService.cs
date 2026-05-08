using eCommerceApi.Models;
using eCommerceApi.Models.Dto;

namespace eCommerceApi.Services
{
    public class OrderQueryParameters
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public interface IOrderService
    {
        Task<PaginatedResponseDto<Order>> GetOrdersAsync(OrderQueryParameters queryParameters);
        Task<Order?> GetOrderByIdAsync(int id);
        Task<Order> CreateOrderAsync(CreateOrderDto createOrderDto);
        Task<Order?> UpdateOrderStatusAsync(int id, OrderStatus newStatus, string? trackingNumber);
    }
}
