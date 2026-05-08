using Microsoft.AspNetCore.SignalR;
using eCommerceApi.Services;

namespace eCommerceApi.Hubs;

public class AdminHub : Hub
{
    private readonly ITotpService _totpService;
    private readonly AdminSessionService _sessionService;
    private readonly ILogger<AdminHub> _logger;

    public AdminHub(ITotpService totpService, AdminSessionService sessionService, ILogger<AdminHub> logger)
    {
        _totpService = totpService;
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task<string?> Authenticate(string totpCode)
    {
        if (!_totpService.ValidateCode(totpCode))
        {
            _logger.LogWarning("Failed admin auth attempt from connection {ConnectionId}.", Context.ConnectionId);
            await Clients.Caller.SendAsync("AuthFailed", "Invalid or expired code.");
            return null;
        }

        var token = _sessionService.CreateSession(Context.ConnectionId);
        _logger.LogInformation("Admin session created for connection {ConnectionId}.", Context.ConnectionId);
        return token;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _sessionService.RevokeByConnectionId(Context.ConnectionId);
        _logger.LogInformation("Admin session revoked for connection {ConnectionId}.", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
