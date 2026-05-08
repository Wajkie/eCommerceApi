using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using eCommerceApi.Data;
using Microsoft.EntityFrameworkCore;

namespace eCommerceApi.Services;

public class WebhookDispatchService : IWebhookDispatchService, IHostedService
{
    private readonly Channel<WebhookEvent> _channel = Channel.CreateUnbounded<WebhookEvent>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDispatchService> _logger;
    private readonly ConcurrentQueue<WebhookDeliveryRecord> _deliveryLog = new();

    private static readonly int[] RetryDelaysMs = { 0, 2_000, 8_000 };

    public WebhookDispatchService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDispatchService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public void Enqueue(WebhookEvent evt) => _channel.Writer.TryWrite(evt);

    public IReadOnlyCollection<WebhookDeliveryRecord> GetDeliveryLog() => _deliveryLog.ToArray();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = ProcessQueueAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        return Task.CompletedTask;
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await DispatchWithRetryAsync(evt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error dispatching webhook {EventType} for store {StoreId}.", evt.EventType, evt.StoreId);
            }
        }
    }

    private async Task DispatchWithRetryAsync(WebhookEvent evt)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CentralContext>();
        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == evt.StoreId);

        if (store?.WebhookUrl == null || store.WebhookSecret == null)
        {
            _logger.LogDebug("Store {StoreId} has no WebhookUrl/WebhookSecret — skipping.", evt.StoreId);
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            evt.EventType,
            evt.Payload,
            Timestamp = DateTime.UtcNow
        });
        var signature = ComputeHmacSha256(store.WebhookSecret, payload);

        for (var attempt = 0; attempt < RetryDelaysMs.Length; attempt++)
        {
            if (RetryDelaysMs[attempt] > 0)
                await Task.Delay(RetryDelaysMs[attempt]);

            var success = await TryDeliverAsync(store.WebhookUrl, payload, signature, evt, attempt + 1);
            if (success)
            {
                _deliveryLog.Enqueue(new WebhookDeliveryRecord
                {
                    StoreId = evt.StoreId,
                    EventType = evt.EventType,
                    Url = store.WebhookUrl,
                    Success = true,
                    AttemptCount = attempt + 1,
                    DeliveredAt = DateTime.UtcNow
                });
                return;
            }
        }

        _logger.LogError("All {Attempts} delivery attempts failed for {EventType} to store {StoreId}.",
            RetryDelaysMs.Length, evt.EventType, evt.StoreId);
        _deliveryLog.Enqueue(new WebhookDeliveryRecord
        {
            StoreId = evt.StoreId,
            EventType = evt.EventType,
            Url = store.WebhookUrl,
            Success = false,
            AttemptCount = RetryDelaysMs.Length,
            DeliveredAt = DateTime.UtcNow
        });
    }

    private async Task<bool> TryDeliverAsync(string url, string payload, string signature, WebhookEvent evt, int attempt)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("webhook");
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Webhook-Signature", $"sha256={signature}");
            request.Headers.Add("X-Event-Type", evt.EventType);

            using var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Webhook {EventType} delivered to store {StoreId} on attempt {Attempt}.",
                    evt.EventType, evt.StoreId, attempt);
                return true;
            }

            _logger.LogWarning("Webhook {EventType} for store {StoreId} attempt {Attempt} → HTTP {Status}.",
                evt.EventType, evt.StoreId, attempt, (int)response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webhook {EventType} for store {StoreId} attempt {Attempt} threw an exception.",
                evt.EventType, evt.StoreId, attempt);
            return false;
        }
    }

    public static string ComputeHmacSha256(string secret, string payload)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(data)).ToLowerInvariant();
    }
}
