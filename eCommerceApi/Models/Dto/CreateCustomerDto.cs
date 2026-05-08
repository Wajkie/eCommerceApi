using System.ComponentModel.DataAnnotations;

namespace eCommerceApi.Models.Dto
{
    public class CreateCustomerDto
    {
        [Required]
        public Guid Id { get; set; }
    }
}
