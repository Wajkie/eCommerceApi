using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using eCommerceApi.Data;
using eCommerceApi.Middleware;
using eCommerceApi.Models;

namespace eCommerceApi.Tests.Security;

public class JwtMiddlewareTests
{
    private const string TestJwksUri = "https://test-idp.example.com/.well-known/jwks.json";
    private static readonly Guid TestStoreId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static DbContextOptions<CentralContext> NewCentralOptions() =>
        new DbContextOptionsBuilder<CentralContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static (RsaSecurityKey publicKey, RsaSecurityKey privateKey) GenerateRsaKeys()
    {
        var rsa = RSA.Create(2048);
        var publicRsa = RSA.Create();
        publicRsa.ImportParameters(rsa.ExportParameters(false));
        return (new RsaSecurityKey(publicRsa), new RsaSecurityKey(rsa));
    }

    private static string MakeJwt(RsaSecurityKey signingKey, bool expired = false)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim("sub", "user-123") }),
            NotBefore = expired ? now.AddMinutes(-10) : now,
            Expires = expired ? now.AddMinutes(-2) : now.AddHours(1),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)
        };
        return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
    }

    private static IMemoryCache CacheWithPublicKey(RsaSecurityKey publicKey)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set($"jwks:{TestJwksUri}", (IEnumerable<SecurityKey>)new[] { publicKey }, TimeSpan.FromMinutes(10));
        return cache;
    }

    private static DefaultHttpContext BuildContext(string? authHeader = null, string? storeId = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        if (authHeader != null) ctx.Request.Headers.Authorization = authHeader;
        if (storeId != null) ctx.Request.Headers["X-Store-Id"] = storeId;
        return ctx;
    }

    [Fact]
    public async Task NoAuthorizationHeader_PassesThrough()
    {
        using var db = new CentralContext(NewCentralOptions());
        var nextCalled = false;
        var mw = new JwtTenantMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<JwtTenantMiddleware>.Instance);
        var ctx = BuildContext();

        await mw.InvokeAsync(ctx, db);

        Assert.True(nextCalled);
        Assert.Equal(200, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task BearerToken_ButNoXStoreIdHeader_PassesThrough()
    {
        using var db = new CentralContext(NewCentralOptions());
        var nextCalled = false;
        var mw = new JwtTenantMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<JwtTenantMiddleware>.Instance);
        var ctx = BuildContext(authHeader: "Bearer sometoken");

        await mw.InvokeAsync(ctx, db);

        Assert.True(nextCalled);
        Assert.Equal(200, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task StoreHasNoJwksUri_PassesThrough()
    {
        using var db = new CentralContext(NewCentralOptions());
        db.Stores.Add(new Store { Id = TestStoreId, Name = "TestStore", WebhookSecret = "secret" });
        await db.SaveChangesAsync();

        var nextCalled = false;
        var mw = new JwtTenantMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<JwtTenantMiddleware>.Instance);
        var ctx = BuildContext("Bearer sometoken", TestStoreId.ToString());

        await mw.InvokeAsync(ctx, db);

        Assert.True(nextCalled);
        Assert.Equal(200, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task InvalidToken_Returns401()
    {
        var (publicKey, _) = GenerateRsaKeys();
        using var db = new CentralContext(NewCentralOptions());
        db.Stores.Add(new Store { Id = TestStoreId, Name = "TestStore", JwksUri = TestJwksUri, WebhookSecret = "secret" });
        await db.SaveChangesAsync();

        var nextCalled = false;
        var mw = new JwtTenantMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            CacheWithPublicKey(publicKey),
            NullLogger<JwtTenantMiddleware>.Instance);
        var ctx = BuildContext("Bearer not.a.valid.jwt", TestStoreId.ToString());

        await mw.InvokeAsync(ctx, db);

        Assert.False(nextCalled);
        Assert.Equal(401, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        var (publicKey, privateKey) = GenerateRsaKeys();
        var jwt = MakeJwt(privateKey, expired: true);
        using var db = new CentralContext(NewCentralOptions());
        db.Stores.Add(new Store { Id = TestStoreId, Name = "TestStore", JwksUri = TestJwksUri, WebhookSecret = "secret" });
        await db.SaveChangesAsync();

        var nextCalled = false;
        var mw = new JwtTenantMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            CacheWithPublicKey(publicKey),
            NullLogger<JwtTenantMiddleware>.Instance);
        var ctx = BuildContext($"Bearer {jwt}", TestStoreId.ToString());

        await mw.InvokeAsync(ctx, db);

        Assert.False(nextCalled);
        Assert.Equal(401, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task ValidToken_SetsUserAndPassesThrough()
    {
        var (publicKey, privateKey) = GenerateRsaKeys();
        var jwt = MakeJwt(privateKey);
        using var db = new CentralContext(NewCentralOptions());
        db.Stores.Add(new Store { Id = TestStoreId, Name = "TestStore", JwksUri = TestJwksUri, WebhookSecret = "secret" });
        await db.SaveChangesAsync();

        var nextCalled = false;
        ClaimsPrincipal? capturedUser = null;
        var mw = new JwtTenantMiddleware(
            ctx => { nextCalled = true; capturedUser = ctx.User; return Task.CompletedTask; },
            CacheWithPublicKey(publicKey),
            NullLogger<JwtTenantMiddleware>.Instance);
        var ctx = BuildContext($"Bearer {jwt}", TestStoreId.ToString());

        await mw.InvokeAsync(ctx, db);

        Assert.True(nextCalled);
        Assert.Equal(200, ctx.Response.StatusCode);
        Assert.NotNull(capturedUser);
        Assert.True(capturedUser!.Identity?.IsAuthenticated);
        // JwtSecurityTokenHandler maps "sub" → ClaimTypes.NameIdentifier by default
        Assert.Contains(capturedUser.Claims, c => c.Value == "user-123");
    }

    [Fact]
    public async Task TokenSignedWithWrongKey_Returns401()
    {
        var (publicKey, _) = GenerateRsaKeys();
        var (_, differentPrivateKey) = GenerateRsaKeys();
        var jwt = MakeJwt(differentPrivateKey);
        using var db = new CentralContext(NewCentralOptions());
        db.Stores.Add(new Store { Id = TestStoreId, Name = "TestStore", JwksUri = TestJwksUri, WebhookSecret = "secret" });
        await db.SaveChangesAsync();

        var nextCalled = false;
        var mw = new JwtTenantMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            CacheWithPublicKey(publicKey),
            NullLogger<JwtTenantMiddleware>.Instance);
        var ctx = BuildContext($"Bearer {jwt}", TestStoreId.ToString());

        await mw.InvokeAsync(ctx, db);

        Assert.False(nextCalled);
        Assert.Equal(401, ctx.Response.StatusCode);
    }
}
