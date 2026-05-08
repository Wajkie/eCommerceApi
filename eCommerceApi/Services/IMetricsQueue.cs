namespace eCommerceApi.Services
{
    /// <summary>
    /// Interface for enqueueing metrics/analytics tasks to be processed asynchronously.
    /// Ensures proper error handling and resource cleanup compared to fire-and-forget tasks.
    /// </summary>
    public interface IMetricsQueue
    {
        /// <summary>
        /// Enqueue a store API call metric for asynchronous processing.
        /// </summary>
        /// <param name="storeId">The store ID to record the metric for.</param>
        /// <param name="cancellationToken">Cancellation token to gracefully shut down.</param>
        /// <returns>A task representing the enqueue operation.</returns>
        Task EnqueueAsync(Guid storeId, CancellationToken cancellationToken = default);
    }
}
