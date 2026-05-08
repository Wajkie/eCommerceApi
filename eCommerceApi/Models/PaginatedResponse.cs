namespace eCommerceApi.Models
{
    public class PaginatedResponse<T>
    {
        public int TotalItems { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
        public IEnumerable<T> Data { get; set; } = new List<T>();
    }
}
