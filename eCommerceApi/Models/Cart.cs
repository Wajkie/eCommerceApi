using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eCommerceApi.Models
{
    public class Cart
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid CustomerId { get; set; }

        [Required]
        public Guid StoreId { get; set; }

        public List<CartItem> Items { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(15);

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = CartStatus.Active; // Active, Abandoned, Converted

        // Computed properties
        [NotMapped]
        public decimal Subtotal => Items.Sum(i => i.UnitPrice * i.Quantity);

        [NotMapped]
        public decimal Tax => Subtotal * 0.08m; // 8% tax rate (configurable)

        [NotMapped]
        public decimal Total => Subtotal + Tax;

        [NotMapped]
        public int ItemCount => Items.Sum(i => i.Quantity);

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}
