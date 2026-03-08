using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Prism.Api.Middleware;

/// <summary>
/// Middleware that catches unhandled exceptions and returns a standardized
/// ProblemDetails JSON response with a 500 status code. Prevents exception
/// details from leaking to clients in non-development environments.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly Serilog.ILogger Logger = Log.ForContext<GlobalExceptionMiddleware>();

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalExceptionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public GlobalExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Processes the HTTP request and catches any unhandled exceptions,
    /// converting them to a ProblemDetails response.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        IHostEnvironment? environment = context.RequestServices.GetService<IHostEnvironment>();
        bool isDevelopment = environment?.IsDevelopment() == true;

        var problemDetails = new ProblemDetails
        {
            Status = (int)HttpStatusCode.InternalServerError,
            Title = "An unexpected error occurred",
            Detail = isDevelopment ? exception.Message : "An internal server error has occurred.",
            Instance = context.Request.Path
        };

        if (isDevelopment)
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        string json = JsonSerializer.Serialize(problemDetails, options);
        await context.Response.WriteAsync(json);
    }
}
