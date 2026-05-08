using System.ComponentModel.DataAnnotations;

namespace eCommerceApi.Models.Dto
{
    public class UpdateCartItemRequest
    {
        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; }
    }
}
