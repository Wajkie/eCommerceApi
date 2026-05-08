using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using eCommerceApi.Exceptions;
using eCommerceApi.Infrastructure;

namespace eCommerceApi.Tests;

public class GlobalExceptionHandlerTests
{
    private readonly GlobalExceptionHandler _handler;

    public GlobalExceptionHandlerTests()
    {
        _handler = new GlobalExceptionHandler(new Mock<ILogger<GlobalExceptionHandler>>().Object);
    }

    private static DefaultHttpContext CreateContext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    [Fact]
    public async Task NotFoundException_Returns404()
    {
        var ctx = CreateContext();
        var handled = await _handler.TryHandleAsync(ctx, new NotFoundException("Customer not found."), default);
        Assert.True(handled);
        Assert.Equal(404, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task BadRequestException_Returns400()
    {
        var ctx = CreateContext();
        var handled = await _handler.TryHandleAsync(ctx, new BadRequestException("Quantity must be > 0."), default);
        Assert.True(handled);
        Assert.Equal(400, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task UnhandledException_Returns500()
    {
        var ctx = CreateContext();
        var handled = await _handler.TryHandleAsync(ctx, new System.Exception("Something broke."), default);
        Assert.True(handled);
        Assert.Equal(500, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task AppException_AlwaysReturnsTrue()
    {
        var ctx = CreateContext();
        var handled = await _handler.TryHandleAsync(ctx, new NotFoundException("x"), default);
        Assert.True(handled);
    }

    [Fact]
    public async Task ErrorCode_IsSetAsTitle_ForAppException()
    {
        var ctx = CreateContext();
        await _handler.TryHandleAsync(ctx, new NotFoundException("x"), default);
        ctx.Response.Body.Position = 0;
        var body = await new System.IO.StreamReader(ctx.Response.Body).ReadToEndAsync();
        Assert.Contains("NOT_FOUND", body);
    }

    [Fact]
    public async Task InternalError_Title_ForUnhandledException()
    {
        var ctx = CreateContext();
        await _handler.TryHandleAsync(ctx, new System.Exception("boom"), default);
        ctx.Response.Body.Position = 0;
        var body = await new System.IO.StreamReader(ctx.Response.Body).ReadToEndAsync();
        Assert.Contains("INTERNAL_ERROR", body);
    }
}
