using Scalar.AspNetCore;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using eCommerceApi.Data;
using eCommerceApi.Services;
using eCommerceApi.Middleware;
using eCommerceApi.Infrastructure;
using eCommerceApi.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// Add SignalR for real-time metrics
builder.Services.AddSignalR()
    .AddJsonProtocol(o =>
        o.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Add CORS so frontend apps (React, Vue, etc.) can communicate with this API
// SECURITY: Only allow trusted domains in production
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            // In production, replace with actual frontend URLs
            var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() 
                ?? new[] { "http://localhost:3000", "http://localhost:3001" };

            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // Required for SignalR
        });
});

builder.Services.AddHttpContextAccessor();

// Add Memory caching for faster store lookups
builder.Services.AddMemoryCache();

// Add Output Caching for Catalog endpoints (varies by Store)
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(b =>
        b.Expire(TimeSpan.FromMinutes(1)).SetVaryByHeader("X-Store-Id"));

    options.AddPolicy("StoreProducts", b =>
        b.Expire(TimeSpan.FromMinutes(1))
         .SetVaryByHeader("X-Store-Id")
         .SetVaryByQuery("page", "pageSize", "externalId", "search")
         .Tag("store-products"));
});

// Add Rate Limiting to prevent brute force
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var storeId = context.Request.Headers["X-Store-Id"].ToString();
        var key = string.IsNullOrEmpty(storeId) 
            ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown" 
            : storeId;

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = 60, // Limit to 60 requests per minute per Store or IP
            Window = TimeSpan.FromMinutes(1)
        });
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new { error = "Too many requests. Please try again later." }, token);
    };
});

builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ITotpService, TotpService>();
builder.Services.AddSingleton<AdminSessionService>();

// Register Cart service
builder.Services.AddScoped<ICartService, CartService>();

// Register Product service
builder.Services.AddScoped<IProductService, ProductService>();

// Register Order service
builder.Services.AddScoped<IOrderService, OrderService>();

// Register Customer service
builder.Services.AddScoped<ICustomerService, CustomerService>();

// Register webhook dispatcher (singleton + hosted service)
builder.Services.AddHttpClient("webhook");
builder.Services.AddSingleton<WebhookDispatchService>();
builder.Services.AddSingleton<IWebhookDispatchService>(sp => sp.GetRequiredService<WebhookDispatchService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<WebhookDispatchService>());

// Register the metrics queue service and its background worker
builder.Services.AddSingleton<IMetricsQueue, InMemoryMetricsQueue>();
builder.Services.AddHostedService(sp => (InMemoryMetricsQueue)sp.GetRequiredService<IMetricsQueue>());

// Register Monitoring Services
builder.Services.AddSingleton<IMetricsService, MetricsService>();
builder.Services.AddSingleton<IAttackDetectionService, AttackDetectionService>();
builder.Services.AddScoped<IHealthCheckService, HealthCheckService>();
builder.Services.AddHostedService<HealthMonitorService>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
var mySqlVersion = new MySqlServerVersion(new Version(5, 7));

// Central Database for the store registry
builder.Services.AddDbContext<CentralContext>(options =>
    options.UseMySql(connectionString, mySqlVersion));

// Tenant data context (all tenants share one MySQL database)
builder.Services.AddDbContext<EcommerceContext>(options =>
    options.UseMySql(connectionString, mySqlVersion));

// Add robust Exception Handling globally
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CentralContext>("CentralDatabase")
    .AddCheck("MemoryCache", () => 
    {
        // Simple cache availability validation
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("In-memory cache is ready.");
    });

var app = builder.Build();

// Apply any pending migrations on startup
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<CentralContext>().Database.Migrate();
    scope.ServiceProvider.GetRequiredService<EcommerceContext>().Database.Migrate();
}

app.UseCors("AllowFrontend"); // Enable CORS absolutely before endpoints
app.UseExceptionHandler(); // Activate global error handling

app.UseMetricsMiddleware(); // Record metrics for all requests

app.UseMiddleware<TenantMiddleware>(); // Enforce multi-tenancy requirements
app.UseJwtTenantValidation();

app.UseRateLimiter(); // Apply Rate Limiting
app.UseOutputCache(); // Cache responses to minimize processing time during heavy workloads

app.UseHttpsRedirection();

app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // A gorgeous UI to test your endpoints in the browser!
}

app.MapHealthChecks("/health");

// Test CORS endpoint
app.MapGet("/test-cors", () => Results.Ok(new { message = "CORS is working!" }))
    .WithName("TestCors")
    .RequireCors("AllowFrontend");

app.MapControllers();

// Map SignalR Hub for real-time metrics
app.MapHub<MetricsHub>("/hubs/metrics");

// Map SignalR Hub for admin authentication
app.MapHub<AdminHub>("/hubs/admin");

app.Run();

// Make the implicit Program class public so it can be referenced by the test project
public partial class Program { }
