using System.ComponentModel.DataAnnotations;

namespace eCommerceApi.Models.Dto
{
    public class AddCartItemRequest
    {
        [Required]
        [MaxLength(200)]
        public string ExternalId { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Label { get; set; }

        [Required]
        [Range(0.01, 1_000_000)]
        public decimal UnitPrice { get; set; }

        [Required]
        [Range(1, 10_000)]
        public int Quantity { get; set; }
    }
}
