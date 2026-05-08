namespace eCommerceApi.Middleware;

using eCommerceApi.Services;
using System.Diagnostics;

public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MetricsMiddleware> _logger;

    public MetricsMiddleware(RequestDelegate next, ILogger<MetricsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // IPs that are never subject to DDoS blocking
    private static readonly HashSet<string> _trustedIps = ["127.0.0.1", "::1", "localhost"];

    public async Task InvokeAsync(HttpContext context, IMetricsService metricsService, IAttackDetectionService attackDetectionService)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var storeId = context.Request.Headers.TryGetValue("X-Store-Id", out var storeIdValue)
            ? storeIdValue.ToString()
            : "default";
        var userAgent = context.Request.Headers["User-Agent"].ToString();

        // Check for DDoS — trusted IPs (localhost) are exempt
        var isDDoS = !_trustedIps.Contains(clientIp) && await attackDetectionService.CheckForDDoSAsync(clientIp);
        if (isDDoS)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            _logger.LogWarning("DDoS detected from IP: {ClientIp}", clientIp);

            // Record the blocked request so the dashboard shows attack traffic
            await metricsService.RecordMetricAsync(
                endpoint: context.Request.Path,
                method: context.Request.Method,
                statusCode: StatusCodes.Status429TooManyRequests,
                duration: 0,
                storeId: storeId,
                clientIp: clientIp,
                userAgent: userAgent
            );

            await context.Response.WriteAsync("Rate limit exceeded");
            return;
        }

        attackDetectionService.RecordRequest(clientIp);

        var sw = Stopwatch.StartNew();

        try
        {
            await _next(context);
            sw.Stop();

            await metricsService.RecordMetricAsync(
                endpoint: context.Request.Path,
                method: context.Request.Method,
                statusCode: context.Response.StatusCode,
                duration: sw.ElapsedMilliseconds,
                storeId: storeId,
                clientIp: clientIp,
                userAgent: userAgent
            );

            _logger.LogInformation(
                "{Method} {Path} responded {StatusCode} in {Duration}ms",
                context.Request.Method, context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error in metrics middleware");
            throw;
        }
    }
}

public static class MetricsMiddlewareExtensions
{
    public static IApplicationBuilder UseMetricsMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MetricsMiddleware>();
    }
}
