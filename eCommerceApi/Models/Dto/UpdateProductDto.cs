using System.ComponentModel.DataAnnotations;

namespace eCommerceApi.Models.Dto
{
    public class UpdateProductDto
    {
        [MaxLength(300)]
        public string? Label { get; set; }

        [Required]
        [Range(0.01, 1_000_000)]
        public decimal Price { get; set; }

        [Required]
        [Range(0, 10_000_000)]
        public int StockUnit { get; set; }

        [Range(0, 10_000_000)]
        public int ReorderLevel { get; set; } = 10;

        [Range(0, 10_000_000)]
        public int TargetStockLevel { get; set; } = 50;
    }
}
