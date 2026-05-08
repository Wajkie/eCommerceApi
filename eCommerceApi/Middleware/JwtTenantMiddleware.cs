using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using eCommerceApi.Data;
using Microsoft.EntityFrameworkCore;

namespace eCommerceApi.Middleware;

/// <summary>
/// Validates Bearer tokens for stores that have registered a JwksUri (BYOIDP).
/// Skips validation when no Authorization header is present or the store has no JwksUri.
/// Sets HttpContext.User on success so downstream code can inspect claims.
/// </summary>
public class JwtTenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<JwtTenantMiddleware> _logger;
    private static readonly TimeSpan JwksCacheDuration = TimeSpan.FromMinutes(10);

    public JwtTenantMiddleware(RequestDelegate next, IMemoryCache cache, ILogger<JwtTenantMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, CentralContext centralContext)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader == null || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var storeIdHeader = context.Request.Headers["X-Store-Id"].FirstOrDefault();
        if (!Guid.TryParse(storeIdHeader, out var storeId))
        {
            await _next(context);
            return;
        }

        var store = await centralContext.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == storeId);
        if (store?.JwksUri == null)
        {
            await _next(context);
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();

        try
        {
            var keys = await GetSigningKeysAsync(store.JwksUri);
            var handler = new JwtSecurityTokenHandler();
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keys,
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var principal = handler.ValidateToken(token, validationParams, out _);
            context.User = principal;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning("JWT validation failed for store {StoreId}: {Reason}", storeId, ex.Message);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired token." });
            return;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            // Malformed token — treat as 401, not a server error
            _logger.LogWarning("Malformed token for store {StoreId}: {Reason}", storeId, ex.Message);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired token." });
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during JWT validation for store {StoreId}", storeId);
        }

        await _next(context);
    }

    private async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(string jwksUri)
    {
        var cacheKey = $"jwks:{jwksUri}";

        if (_cache.TryGetValue(cacheKey, out IEnumerable<SecurityKey>? cached) && cached != null)
            return cached;

        var retriever = new HttpDocumentRetriever { RequireHttps = jwksUri.StartsWith("https", StringComparison.OrdinalIgnoreCase) };
        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            jwksUri,
            new OpenIdConnectConfigurationRetriever(),
            retriever);

        // Fetch the JWKS endpoint directly
        var jwks = await GetJwksAsync(jwksUri);
        _cache.Set(cacheKey, jwks, JwksCacheDuration);
        return jwks;
    }

    private static async Task<IEnumerable<SecurityKey>> GetJwksAsync(string jwksUri)
    {
        using var http = new HttpClient();
        var json = await http.GetStringAsync(jwksUri);
        var jwkSet = new JsonWebKeySet(json);
        return jwkSet.GetSigningKeys();
    }
}

public static class JwtTenantMiddlewareExtensions
{
    public static IApplicationBuilder UseJwtTenantValidation(this IApplicationBuilder app) =>
        app.UseMiddleware<JwtTenantMiddleware>();
}
