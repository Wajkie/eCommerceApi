using System.Collections.Concurrent;
using eCommerceApi.Data;

namespace eCommerceApi.Services
{
    /// <summary>
    /// In-memory implementation of IMetricsQueue using a background worker.
    /// Processes store API call metrics asynchronously with proper error handling and graceful shutdown.
    /// </summary>
    public class InMemoryMetricsQueue : IMetricsQueue, IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<InMemoryMetricsQueue> _logger;
        private readonly ConcurrentQueue<Guid> _queue = new();
        private readonly SemaphoreSlim _semaphore = new(0);
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _processingTask;

        public InMemoryMetricsQueue(IServiceProvider serviceProvider, ILogger<InMemoryMetricsQueue> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Enqueue a store ID for metrics processing.
        /// </summary>
        public Task EnqueueAsync(Guid storeId, CancellationToken cancellationToken = default)
        {
            _queue.Enqueue(storeId);
            _semaphore.Release();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Start the background worker on application startup.
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _processingTask = ProcessQueueAsync(_cancellationTokenSource.Token);

            _logger.LogInformation("InMemoryMetricsQueue background worker started.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop the background worker on application shutdown.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping InMemoryMetricsQueue background worker...");

            _cancellationTokenSource?.Cancel();

            if (_processingTask != null)
            {
                try
                {
                    // Wait up to 30 seconds for pending tasks to complete
                    await _processingTask.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("InMemoryMetricsQueue background worker did not complete within timeout period.");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("InMemoryMetricsQueue background worker was cancelled.");
                }
            }

            _cancellationTokenSource?.Dispose();
            _semaphore?.Dispose();

            _logger.LogInformation("InMemoryMetricsQueue background worker stopped.");
        }

        /// <summary>
        /// Process queued metrics updates.
        /// </summary>
        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for an item to be enqueued or cancellation
                    await _semaphore.WaitAsync(cancellationToken);

                    if (!_queue.TryDequeue(out var storeId))
                    {
                        continue;
                    }

                    // Process the metric update
                    await ProcessMetricAsync(storeId, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("InMemoryMetricsQueue processing was cancelled.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in InMemoryMetricsQueue processing.");
                }
            }
        }

        /// <summary>
        /// Process a single metric update for a store.
        /// </summary>
        private async Task ProcessMetricAsync(Guid storeId, CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateAsyncScope();
                var centralContext = scope.ServiceProvider.GetRequiredService<CentralContext>();

                var store = await centralContext.Stores.FindAsync(new object[] { storeId }, cancellationToken: cancellationToken);
                if (store != null)
                {
                    store.TotalApiCalls++;
                    store.LastApiCallAt = DateTime.UtcNow;
                    await centralContext.SaveChangesAsync(cancellationToken);

                    _logger.LogDebug("Updated metrics for store {StoreId}: Total API calls = {TotalCalls}", storeId, store.TotalApiCalls);
                }
                else
                {
                    _logger.LogWarning("Store {StoreId} not found for metrics update.", storeId);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Metrics update for store {StoreId} was cancelled.", storeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating metrics for store {StoreId}.", storeId);
            }
        }
    }
}
