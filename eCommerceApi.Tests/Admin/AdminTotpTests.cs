using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OtpNet;
using Xunit;
using eCommerceApi.Controllers;
using eCommerceApi.Data;
using eCommerceApi.Filters;
using eCommerceApi.Models;
using eCommerceApi.Services;

namespace eCommerceApi.Tests.Admin;

public class AdminTotpTests
{
    private static DbContextOptions<CentralContext> NewOptions() =>
        new DbContextOptionsBuilder<CentralContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    // --- AdminController.Setup ---

    [Fact]
    public void Setup_WhenNotConfigured_ReturnsOkWithQrUri()
    {
        using var ctx = new CentralContext(NewOptions());
        var controller = new AdminController(ctx, new Mock<ILogger<AdminController>>().Object);

        var result = controller.Setup();

        var ok = Assert.IsType<OkObjectResult>(result);
        var type = ok.Value!.GetType();
        var qrUri = type.GetProperty("QrUri")!.GetValue(ok.Value) as string;
        Assert.NotNull(qrUri);
        Assert.StartsWith("otpauth://totp/", qrUri);
        Assert.Single(ctx.AdminConfig.ToList());
    }

    [Fact]
    public void Setup_WhenAlreadyConfigured_ReturnsConflict()
    {
        using var ctx = new CentralContext(NewOptions());
        ctx.AdminConfig.Add(new AdminConfig { TotpSecret = "JBSWY3DPEHPK3PXP", CreatedAt = DateTime.UtcNow });
        ctx.SaveChanges();
        var controller = new AdminController(ctx, new Mock<ILogger<AdminController>>().Object);

        var result = controller.Setup();

        Assert.IsType<ConflictObjectResult>(result);
    }

    // --- TotpService ---

    [Fact]
    public void IsConfigured_ReturnsFalse_WhenNoSecret()
    {
        using var ctx = new CentralContext(NewOptions());
        var service = new TotpService(ctx);
        Assert.False(service.IsConfigured());
    }

    [Fact]
    public void IsConfigured_ReturnsTrue_WhenSecretExists()
    {
        using var ctx = new CentralContext(NewOptions());
        ctx.AdminConfig.Add(new AdminConfig { TotpSecret = "JBSWY3DPEHPK3PXP", CreatedAt = DateTime.UtcNow });
        ctx.SaveChanges();
        var service = new TotpService(ctx);
        Assert.True(service.IsConfigured());
    }

    [Fact]
    public void ValidateCode_WithCorrectCode_ReturnsTrue()
    {
        using var ctx = new CentralContext(NewOptions());
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secretBytes);
        ctx.AdminConfig.Add(new AdminConfig { TotpSecret = base32Secret, CreatedAt = DateTime.UtcNow });
        ctx.SaveChanges();

        var currentCode = new Totp(secretBytes).ComputeTotp();
        var service = new TotpService(ctx);

        Assert.True(service.ValidateCode(currentCode));
    }

    [Fact]
    public void ValidateCode_WithWrongCode_ReturnsFalse()
    {
        using var ctx = new CentralContext(NewOptions());
        ctx.AdminConfig.Add(new AdminConfig { TotpSecret = "JBSWY3DPEHPK3PXP", CreatedAt = DateTime.UtcNow });
        ctx.SaveChanges();
        var service = new TotpService(ctx);

        Assert.False(service.ValidateCode("000000"));
    }

    [Fact]
    public void ValidateCode_WithNoConfig_ReturnsFalse()
    {
        using var ctx = new CentralContext(NewOptions());
        var service = new TotpService(ctx);
        Assert.False(service.ValidateCode("123456"));
    }

    // --- RequireTotpAuthAttribute ---

    [Fact]
    public async Task Filter_WithMissingHeader_ReturnsUnauthorized()
    {
        var mockTotp = new Mock<ITotpService>();
        var services = new ServiceCollection();
        services.AddSingleton(mockTotp.Object);
        var httpCtx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        var actionContext = new ActionContext(httpCtx, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());

        var filter = new RequireTotpAuthAttribute();
        await filter.OnActionExecutionAsync(context, () => throw new Exception("should not reach"));

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public async Task Filter_WithInvalidCode_ReturnsUnauthorized()
    {
        var mockTotp = new Mock<ITotpService>();
        mockTotp.Setup(s => s.ValidateCode("999999")).Returns(false);

        var services = new ServiceCollection();
        services.AddSingleton(mockTotp.Object);
        var httpCtx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        httpCtx.Request.Headers["X-Admin-Code"] = "999999";

        var actionContext = new ActionContext(httpCtx, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());

        var filter = new RequireTotpAuthAttribute();
        await filter.OnActionExecutionAsync(context, () => throw new Exception("should not reach"));

        Assert.IsType<UnauthorizedObjectResult>(context.Result);
    }

    [Fact]
    public async Task Filter_WithValidCode_CallsNext()
    {
        var mockTotp = new Mock<ITotpService>();
        mockTotp.Setup(s => s.ValidateCode("123456")).Returns(true);

        var services = new ServiceCollection();
        services.AddSingleton(mockTotp.Object);
        var httpCtx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        httpCtx.Request.Headers["X-Admin-Code"] = "123456";

        var actionContext = new ActionContext(httpCtx, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());

        var nextCalled = false;
        var filter = new RequireTotpAuthAttribute();
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        });

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }
}
