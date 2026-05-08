namespace eCommerceApi.Services;

public interface ITotpService
{
    bool IsConfigured();
    bool ValidateCode(string code);
}
