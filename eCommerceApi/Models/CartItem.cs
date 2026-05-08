using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eCommerceApi.Models
{
    public class CartItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid CartId { get; set; }

        public int ProductId { get; set; }

        [Required]
        [MaxLength(200)]
        public string ExternalId { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Label { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        public decimal UnitPrice { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(CartId))]
        public Cart? Cart { get; set; }

        [NotMapped]
        public decimal TotalPrice => UnitPrice * Quantity;
    }
}
