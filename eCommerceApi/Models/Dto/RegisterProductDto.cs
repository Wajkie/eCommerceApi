using System.ComponentModel.DataAnnotations;

namespace eCommerceApi.Models.Dto
{
    public class RegisterProductDto
    {
        [Required]
        [MaxLength(200)]
        public string ExternalId { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Label { get; set; }

        [Required]
        [Range(0.01, 1_000_000)]
        public decimal Price { get; set; }

        [Range(0, 10_000_000)]
        public int StockUnit { get; set; }
    }
}
