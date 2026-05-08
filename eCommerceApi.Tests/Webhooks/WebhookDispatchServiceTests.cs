using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using eCommerceApi.Data;
using eCommerceApi.Models;
using eCommerceApi.Services;

namespace eCommerceApi.Tests.Webhooks;

public class WebhookDispatchServiceTests
{
    private static readonly Guid StoreId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private const string WebhookSecret = "test-secret-key";
    private const string WebhookUrl = "http://fake-endpoint.test/webhook";

    private static ServiceProvider BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<CentralContext>(opt => opt.UseInMemoryDatabase(dbName));
        return services.BuildServiceProvider();
    }

    private static (WebhookDispatchService service, ServiceProvider provider, FakeHttpHandler handler) Build(
        string dbName,
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        var provider = BuildProvider(dbName);
        var fakeHandler = new FakeHttpHandler(respond ?? (_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)));
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient("webhook"))
            .Returns(new HttpClient(fakeHandler) { Timeout = TimeSpan.FromSeconds(5) });

        var service = new WebhookDispatchService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            mockFactory.Object,
            NullLogger<WebhookDispatchService>.Instance);

        return (service, provider, fakeHandler);
    }

    [Fact]
    public async Task EnqueueAndDispatch_StoreWithWebhook_PostsPayload()
    {
        var (svc, provider, handler) = Build(Guid.NewGuid().ToString());
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CentralContext>();
            db.Stores.Add(new Store { Id = StoreId, Name = "S", WebhookUrl = WebhookUrl, WebhookSecret = WebhookSecret });
            await db.SaveChangesAsync();
        }

        var dispatched = new TaskCompletionSource<HttpRequestMessage>();
        handler.OnRequest = req => dispatched.TrySetResult(req);

        await svc.StartAsync(CancellationToken.None);
        svc.Enqueue(new WebhookEvent(StoreId, "order.created", new { OrderId = 42 }));

        var request = await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await svc.StopAsync(CancellationToken.None);

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(WebhookUrl, request.RequestUri!.ToString());
        Assert.True(request.Headers.Contains("X-Webhook-Signature"));
        Assert.Equal("order.created", request.Headers.GetValues("X-Event-Type").First());
    }

    [Fact]
    public async Task EnqueueAndDispatch_SignatureIsCorrectHmacSha256()
    {
        var (svc, provider, handler) = Build(Guid.NewGuid().ToString());
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CentralContext>();
            db.Stores.Add(new Store { Id = StoreId, Name = "S", WebhookUrl = WebhookUrl, WebhookSecret = WebhookSecret });
            await db.SaveChangesAsync();
        }

        string? capturedPayload = null;
        string? capturedSignatureHeader = null;
        var dispatched = new TaskCompletionSource<bool>();
        handler.OnRequest = req =>
        {
            capturedPayload = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            capturedSignatureHeader = req.Headers.GetValues("X-Webhook-Signature").First();
            dispatched.TrySetResult(true);
        };

        await svc.StartAsync(CancellationToken.None);
        svc.Enqueue(new WebhookEvent(StoreId, "order.created", new { OrderId = 42 }));
        await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await svc.StopAsync(CancellationToken.None);

        var expectedSig = WebhookDispatchService.ComputeHmacSha256(WebhookSecret, capturedPayload!);
        Assert.Equal($"sha256={expectedSig}", capturedSignatureHeader);
    }

    [Fact]
    public async Task EnqueueAndDispatch_StoreWithNoWebhookUrl_NoHttpCall()
    {
        var (svc, provider, handler) = Build(Guid.NewGuid().ToString());
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CentralContext>();
            db.Stores.Add(new Store { Id = StoreId, Name = "S", WebhookSecret = WebhookSecret });
            await db.SaveChangesAsync();
        }

        await svc.StartAsync(CancellationToken.None);
        svc.Enqueue(new WebhookEvent(StoreId, "order.created", new { }));

        // Give it enough time to process (but there's nothing to wait on, so wait briefly)
        await Task.Delay(200);
        await svc.StopAsync(CancellationToken.None);

        Assert.Empty(handler.SentRequests);
    }

    [Fact]
    public async Task EnqueueAndDispatch_OnTransientFailure_RetriesAndSucceeds()
    {
        var callCount = 0;
        var (svc, provider, _) = Build(Guid.NewGuid().ToString(), req =>
        {
            callCount++;
            return callCount < 2
                ? new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CentralContext>();
            db.Stores.Add(new Store { Id = StoreId, Name = "S", WebhookUrl = WebhookUrl, WebhookSecret = WebhookSecret });
            await db.SaveChangesAsync();
        }

        await svc.StartAsync(CancellationToken.None);
        svc.Enqueue(new WebhookEvent(StoreId, "order.created", new { }));

        // Wait for retry (first attempt fails immediately, second after 2s delay)
        await Task.Delay(3_500);
        await svc.StopAsync(CancellationToken.None);

        Assert.Equal(2, callCount);
        var log = svc.GetDeliveryLog();
        Assert.Single(log);
        Assert.True(log.First().Success);
        Assert.Equal(2, log.First().AttemptCount);
    }

    [Fact]
    public async Task EnqueueAndDispatch_AllAttemptsFailure_LogsFailure()
    {
        var (svc, provider, handler) = Build(Guid.NewGuid().ToString(),
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CentralContext>();
            db.Stores.Add(new Store { Id = StoreId, Name = "S", WebhookUrl = WebhookUrl, WebhookSecret = WebhookSecret });
            await db.SaveChangesAsync();
        }

        await svc.StartAsync(CancellationToken.None);
        svc.Enqueue(new WebhookEvent(StoreId, "order.created", new { }));

        // Wait for all 3 attempts: 0 + 2s + 8s = ~10s; use longer timeout
        await Task.Delay(11_000);
        await svc.StopAsync(CancellationToken.None);

        Assert.Equal(3, handler.SentRequests.Count);
        var log = svc.GetDeliveryLog();
        Assert.Single(log);
        Assert.False(log.First().Success);
    }

    [Fact]
    public void ComputeHmacSha256_ProducesCorrectSignature()
    {
        var secret = "mysecret";
        var payload = "hello world";
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(key);
        var expected = Convert.ToHexString(hmac.ComputeHash(data)).ToLowerInvariant();

        var result = WebhookDispatchService.ComputeHmacSha256(secret, payload);

        Assert.Equal(expected, result);
    }
}

public class FakeHttpHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> SentRequests { get; } = new();
    public Action<HttpRequestMessage>? OnRequest { get; set; }

    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SentRequests.Add(request);
        OnRequest?.Invoke(request);
        return Task.FromResult(_respond(request));
    }
}
