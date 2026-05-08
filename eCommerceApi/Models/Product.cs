using System.ComponentModel.DataAnnotations;

namespace eCommerceApi.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        public Guid StoreId { get; set; }

        [Required]
        [MaxLength(200)]
        public string ExternalId { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Label { get; set; }

        public decimal Price { get; set; }

        public int StockUnit { get; set; }

        public int ReorderLevel { get; set; } = 10;

        public int TargetStockLevel { get; set; } = 50;

        public bool NeedsReorder => StockUnit <= ReorderLevel;
    }
}
