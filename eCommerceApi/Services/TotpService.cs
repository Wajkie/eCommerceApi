using OtpNet;
using eCommerceApi.Data;

namespace eCommerceApi.Services;

public class TotpService : ITotpService
{
    private readonly CentralContext _context;

    public TotpService(CentralContext context)
    {
        _context = context;
    }

    public bool IsConfigured() => _context.AdminConfig.Any();

    public bool ValidateCode(string code)
    {
        var config = _context.AdminConfig.FirstOrDefault();
        if (config == null) return false;

        var secretBytes = Base32Encoding.ToBytes(config.TotpSecret);
        var totp = new Totp(secretBytes);
        return totp.VerifyTotp(DateTime.UtcNow, code, out _, new VerificationWindow(previous: 1, future: 1));
    }
}
