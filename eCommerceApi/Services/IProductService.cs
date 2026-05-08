using eCommerceApi.Models;
using eCommerceApi.Models.Dto;

namespace eCommerceApi.Services
{
    public class ProductQueryParameters
    {
        public string? ExternalId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public interface IProductService
    {
        Task<PaginatedResponse<Product>> GetProductsAsync(ProductQueryParameters queryParameters);
        Task<Product?> GetProductByIdAsync(int id);
        Task<Product?> GetProductByExternalIdAsync(string externalId);
        Task<IEnumerable<Product>> BulkRegisterAsync(IEnumerable<RegisterProductDto> items);
        Task<bool> UpdateProductAsync(int id, UpdateProductDto dto);
        Task<bool> DeleteProductAsync(int id);
    }
}
