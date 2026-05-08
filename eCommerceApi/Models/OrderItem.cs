using System.ComponentModel.DataAnnotations;

namespace eCommerceApi.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public int ProductId { get; set; }

        [Required]
        [MaxLength(200)]
        public string ExternalId { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Label { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }
    }
}
