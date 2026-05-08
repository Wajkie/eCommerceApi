using Microsoft.AspNetCore.Mvc;
using OtpNet;
using eCommerceApi.Data;
using eCommerceApi.Models;
using eCommerceApi.Services;

namespace eCommerceApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly CentralContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(CentralContext context, ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// One-time setup. Generates a TOTP secret and returns the QR URI to scan with Google Authenticator.
    /// This endpoint disables itself after the first successful call.
    /// </summary>
    [HttpPost("setup")]
    public IActionResult Setup()
    {
        if (_context.AdminConfig.Any())
            return Conflict(new { error = "Admin TOTP is already configured. Remove the AdminConfig row manually to reset." });

        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secretBytes);

        _context.AdminConfig.Add(new AdminConfig
        {
            TotpSecret = base32Secret,
            CreatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();

        var label = Uri.EscapeDataString("eCommerceApi:admin");
        var qrUri = $"otpauth://totp/{label}?secret={base32Secret}&issuer=eCommerceApi&algorithm=SHA1&digits=6&period=30";

        _logger.LogInformation("Admin TOTP configured at {Time}.", DateTime.UtcNow);

        return Ok(new
        {
            Message = "Scan the QR URI with Google Authenticator. This endpoint is now locked.",
            QrUri = qrUri,
            Secret = base32Secret
        });
    }
}
