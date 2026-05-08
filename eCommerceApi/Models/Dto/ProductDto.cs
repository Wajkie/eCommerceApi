namespace eCommerceApi.Models.Dto
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string? Label { get; set; }
        public decimal Price { get; set; }
        public int StockUnit { get; set; }
        public bool NeedsReorder { get; set; }
    }
}
