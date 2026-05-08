namespace eCommerceApi.Models.Dto
{
    public class CartDto
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Guid StoreId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public int ItemCount { get; set; }
        public List<CartItemDto> Items { get; set; } = new();
    }
}
