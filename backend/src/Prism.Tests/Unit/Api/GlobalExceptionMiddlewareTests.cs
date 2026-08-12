using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prism.Api.Middleware;

namespace Prism.Tests.Unit.Api;

/// <summary>
/// Proofs for the global exception handler's status-code and disclosure behaviour.
/// </summary>
/// <remarks>
/// A malformed request body reaches the pipeline as <see cref="BadHttpRequestException"/>
/// (status 400). The handler used to force every exception to 500, so a garbled payload — the
/// caller's mistake — was reported as a server crash, and in development a stack trace rode
/// along on it. Both are wrong for a 4xx.
/// </remarks>
public sealed class GlobalExceptionMiddlewareTests
{
    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Prism.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static DefaultHttpContext BuildContext(bool development)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new StubHostEnvironment
        {
            EnvironmentName = development ? Environments.Development : Environments.Production,
        });

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };
        context.Request.Path = "/api/v1/playground/chat";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<(int Status, JsonElement Body)> RunAsync(
        RequestDelegate next, bool development)
    {
        DefaultHttpContext context = BuildContext(development);
        var middleware = new GlobalExceptionMiddleware(next);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        string json = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        using var document = JsonDocument.Parse(json);
        return (context.Response.StatusCode, document.RootElement.Clone());
    }

    /// <summary>
    /// A bad request body becomes a 400 carrying the binder's own message, and — even in
    /// development — no stack trace, because there is no server bug to diagnose.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Bad_Request_Body_Is_A_400_Without_A_Stack_Trace(bool development)
    {
        (int status, JsonElement body) = await RunAsync(
            _ => throw new BadHttpRequestException("Failed to read the request body as JSON.", 400),
            development);

        Assert.Equal(400, status);
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.Equal("Bad request", body.GetProperty("title").GetString());
        // The caller's mistake is described verbatim regardless of environment.
        Assert.Contains("request body", body.GetProperty("detail").GetString()!);
        Assert.False(body.TryGetProperty("stackTrace", out _), "A 4xx must not leak a stack trace.");
    }

    /// <summary>
    /// A genuine server fault stays a 500, and its message is hidden outside development.
    /// </summary>
    [Fact]
    public async Task Server_Fault_Is_A_500_And_Hides_Its_Message_In_Production()
    {
        (int status, JsonElement body) = await RunAsync(
            _ => throw new InvalidOperationException("secret internal detail"),
            development: false);

        Assert.Equal(500, status);
        Assert.Equal("An unexpected error occurred", body.GetProperty("title").GetString());
        Assert.Equal("An internal server error has occurred.", body.GetProperty("detail").GetString());
        Assert.DoesNotContain("secret", body.GetProperty("detail").GetString()!);
        Assert.False(body.TryGetProperty("stackTrace", out _));
    }

    /// <summary>
    /// In development a 500 surfaces its message and stack trace for diagnosis.
    /// </summary>
    [Fact]
    public async Task Server_Fault_Surfaces_Detail_In_Development()
    {
        (int status, JsonElement body) = await RunAsync(
            _ => throw new InvalidOperationException("secret internal detail"),
            development: true);

        Assert.Equal(500, status);
        Assert.Equal("secret internal detail", body.GetProperty("detail").GetString());
        Assert.True(body.TryGetProperty("stackTrace", out _));
    }

    /// <summary>
    /// A request that does not throw passes through untouched.
    /// </summary>
    [Fact]
    public async Task Successful_Request_Passes_Through()
    {
        DefaultHttpContext context = BuildContext(development: false);
        var middleware = new GlobalExceptionMiddleware(_ =>
        {
            context.Response.StatusCode = 204;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(204, context.Response.StatusCode);
    }
}
