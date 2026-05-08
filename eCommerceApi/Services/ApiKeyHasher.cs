using System.Security.Cryptography;
using System.Text;

namespace eCommerceApi.Services;

public static class ApiKeyHasher
{
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string rawKey, string serverSecret)
    {
        var key = Encoding.UTF8.GetBytes(serverSecret);
        var data = Encoding.UTF8.GetBytes(rawKey);
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(data)).ToLowerInvariant();
    }
}
