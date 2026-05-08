using System.ComponentModel.DataAnnotations;

namespace eCommerceApi.Models.Dto
{
    public class CreateOrderDto
    {
        [Required]
        public Guid CustomerId { get; set; }

        [Required]
        [MinLength(1)]
        public List<CreateOrderItemDto> Items { get; set; } = new();

        public Guid? IdempotencyKey { get; set; }
    }
}
