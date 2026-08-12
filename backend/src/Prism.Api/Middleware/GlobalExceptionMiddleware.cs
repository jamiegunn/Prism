using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
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
        catch (BadHttpRequestException ex)
        {
            // A malformed or missing request body is the caller's mistake, not the server's.
            // ASP.NET Core raises this (with StatusCode 400) when it cannot bind the body —
            // routing it through the generic handler reported every garbled payload as a 500,
            // which reads as "the server crashed" and pages the wrong person.
            Logger.Information(ex, "Bad request body on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await HandleExceptionAsync(context, ex, ex.StatusCode, "The request could not be read.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await HandleExceptionAsync(context, ex, (int)HttpStatusCode.InternalServerError,
                "An internal server error has occurred.");
        }
    }

    private static async Task HandleExceptionAsync(
        HttpContext context, Exception exception, int statusCode, string publicDetail)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        IHostEnvironment? environment = context.RequestServices.GetService<IHostEnvironment>();
        bool isDevelopment = environment?.IsDevelopment() == true;

        bool isClientError = statusCode is >= 400 and < 500;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = isClientError ? "Bad request" : "An unexpected error occurred",
            // A 4xx detail describes the caller's mistake and is safe to return verbatim; a
            // 5xx message could carry internals, so it stays generic outside development.
            Detail = (isClientError || isDevelopment) ? exception.Message : publicDetail,
            Instance = context.Request.Path
        };

        // Never leak a stack trace on a client error, even in development — there is no server
        // bug to diagnose, and it is noise the caller cannot act on.
        if (isDevelopment && !isClientError)
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
