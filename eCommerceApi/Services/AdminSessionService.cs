using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace eCommerceApi.Services;

public class AdminSessionService
{
    // connectionId → sessionToken
    private readonly ConcurrentDictionary<string, string> _byConnection = new();
    // sessionToken → connectionId (reverse for O(1) validation)
    private readonly ConcurrentDictionary<string, string> _byToken = new();

    public string CreateSession(string connectionId)
    {
        // Clean up any existing session for this connection first
        RevokeByConnectionId(connectionId);

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _byConnection[connectionId] = token;
        _byToken[token] = connectionId;
        return token;
    }

    public bool ValidateSession(string token) => _byToken.ContainsKey(token);

    public void RevokeByConnectionId(string connectionId)
    {
        if (_byConnection.TryRemove(connectionId, out var token))
            _byToken.TryRemove(token, out _);
    }
}
