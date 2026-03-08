using System.Diagnostics;
using Serilog;

namespace Prism.Api.Middleware;

/// <summary>
/// Middleware that logs the start and completion of each HTTP request
/// with structured properties including method, path, status code, and elapsed time.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly Serilog.ILogger Logger = Log.ForContext<RequestLoggingMiddleware>();

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLoggingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public RequestLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Processes the HTTP request, logging the start and completion with timing information.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        string method = context.Request.Method;
        string path = context.Request.Path;

        Logger.Debug("Request started {Method} {Path}", method, path);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            int statusCode = context.Response.StatusCode;
            long elapsedMs = stopwatch.ElapsedMilliseconds;

            Logger.Information("Request completed {Method} {Path} with {StatusCode} in {ElapsedMs}ms",
                method, path, statusCode, elapsedMs);
        }
    }
}
