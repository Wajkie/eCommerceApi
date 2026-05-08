namespace eCommerceApi.Models
{
    public class Order
    {
        public int Id { get; set; }

        public Guid StoreId { get; set; }

        public Guid? IdempotencyKey { get; set; }

        public DateTime OrderDate { get; set; }

        public Guid CustomerId { get; set; }

        public Customer? Customer { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public string? TrackingNumber { get; set; }

        public decimal TotalAmount { get; set; }

        public List<OrderItem> Items { get; set; } = new();
    }
}