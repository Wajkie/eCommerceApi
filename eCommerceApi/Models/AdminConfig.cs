namespace eCommerceApi.Models;

public class AdminConfig
{
    public int Id { get; set; }
    public string TotpSecret { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
