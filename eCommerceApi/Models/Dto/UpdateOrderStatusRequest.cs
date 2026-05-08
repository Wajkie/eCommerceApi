using System.ComponentModel.DataAnnotations;

namespace eCommerceApi.Models.Dto
{
    public class UpdateOrderStatusRequest
    {
        [Required]
        public OrderStatus NewStatus { get; set; }

        public string? TrackingNumber { get; set; }
    }
}
