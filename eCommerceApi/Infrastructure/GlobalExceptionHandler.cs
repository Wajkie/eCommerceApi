using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using eCommerceApi.Exceptions;

namespace eCommerceApi.Infrastructure;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        int statusCode;
        string title;

        if (exception is AppException appEx)
        {
            statusCode = appEx.StatusCode;
            title = appEx.ErrorCode;

            if (statusCode >= 500)
                _logger.LogError(exception, "Server error {ErrorCode}: {Message}", appEx.ErrorCode, exception.Message);
            else
                _logger.LogWarning("Client error {StatusCode} {ErrorCode}: {Message}", statusCode, appEx.ErrorCode, exception.Message);
        }
        else
        {
            statusCode = StatusCodes.Status500InternalServerError;
            title = "INTERNAL_ERROR";
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message
        }, cancellationToken);

        return true;
    }
}
