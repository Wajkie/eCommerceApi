namespace eCommerceApi.Models.Dto
{
    public class OrderItemDto
    {
        public int Id { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string? Label { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
