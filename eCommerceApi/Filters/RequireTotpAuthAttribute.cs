using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using eCommerceApi.Services;

namespace eCommerceApi.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequireTotpAuthAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var sessionService = context.HttpContext.RequestServices.GetRequiredService<AdminSessionService>();
        var totpService = context.HttpContext.RequestServices.GetRequiredService<ITotpService>();

        // Session token from dashboard WebSocket session (preferred)
        if (context.HttpContext.Request.Headers.TryGetValue("X-Admin-Session", out var token)
            && !string.IsNullOrEmpty(token)
            && sessionService.ValidateSession(token!))
        {
            await next();
            return;
        }

        // One-time TOTP code fallback (Scalar UI, Postman, scripts)
        if (context.HttpContext.Request.Headers.TryGetValue("X-Admin-Code", out var code)
            && !string.IsNullOrEmpty(code)
            && totpService.ValidateCode(code!))
        {
            await next();
            return;
        }

        context.Result = new UnauthorizedObjectResult(new
        {
            error = "Provide a valid X-Admin-Session header (dashboard) or X-Admin-Code header (TOTP)."
        });
    }
}
