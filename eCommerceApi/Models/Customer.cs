namespace eCommerceApi.Models
{
    public class Customer
    {
        public Guid Id { get; set; } // UUID provided by an external IDP (e.g. Auth0, Azure AD B2C)

        public Guid StoreId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<Order> Orders { get; set; } = new();
    }
}