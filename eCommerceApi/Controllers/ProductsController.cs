using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using eCommerceApi.Models;
using eCommerceApi.Services;
using eCommerceApi.Models.Dto;

namespace eCommerceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(IProductService productService, ILogger<ProductsController> logger)
        {
            _productService = productService;
            _logger = logger;
        }

        [HttpGet]
        [OutputCache(Duration = 60,
            VaryByHeaderNames = new[] { "X-Store-Id" },
            VaryByQueryKeys = new[] { "externalId", "page", "pageSize" })]
        public async Task<ActionResult<PaginatedResponseDto<ProductDto>>> GetProducts([FromQuery] ProductQueryParameters queryParameters)
        {
            var paginatedResult = await _productService.GetProductsAsync(queryParameters);
            var paginatedDto = new PaginatedResponseDto<ProductDto>
            {
                TotalItems = paginatedResult.TotalItems,
                PageSize = paginatedResult.PageSize,
                PageNumber = paginatedResult.PageNumber,
                TotalPages = paginatedResult.TotalPages,
                Data = paginatedResult.Data.Select(MapToDto)
            };
            return Ok(paginatedDto);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProductDto>> GetProduct(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null)
                return NotFound(new { error = $"Product {id} not found." });

            return Ok(MapToDto(product));
        }

        [HttpPost("bulk")]
        public async Task<ActionResult<IEnumerable<ProductDto>>> BulkRegister([FromBody] List<RegisterProductDto> items)
        {
            if (items == null || items.Count == 0)
                return BadRequest(new { error = "At least one product is required." });

            var created = await _productService.BulkRegisterAsync(items);
            return Ok(created.Select(MapToDto));
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpdateProductDto dto)
        {
            var result = await _productService.UpdateProductAsync(id, dto);
            if (!result)
                return NotFound(new { error = $"Product {id} not found." });

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var result = await _productService.DeleteProductAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }

        private static ProductDto MapToDto(Product product) => new()
        {
            Id = product.Id,
            ExternalId = product.ExternalId,
            Label = product.Label,
            Price = product.Price,
            StockUnit = product.StockUnit,
            NeedsReorder = product.NeedsReorder
        };
    }
}
