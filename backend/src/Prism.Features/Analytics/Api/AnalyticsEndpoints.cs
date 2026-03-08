using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Prism.Common.Results;
using Prism.Features.Analytics.Application.Dtos;
using Prism.Features.Analytics.Application.GetPerformance;
using Prism.Features.Analytics.Application.GetUsage;

namespace Prism.Features.Analytics.Api;

/// <summary>
/// Defines the HTTP endpoints for analytics and usage tracking.
/// </summary>
public static class AnalyticsEndpoints
{
    /// <summary>
    /// Maps the analytics endpoints under <c>/api/v1/analytics</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/analytics")
            .WithTags("Analytics");

        group.MapGet("/usage", GetUsage)
            .WithName("GetUsage")
            .WithSummary("Gets usage statistics with optional filters")
            .Produces<UsageSummaryDto>();

        group.MapGet("/performance", GetPerformance)
            .WithName("GetPerformance")
            .WithSummary("Gets performance metrics with latency percentiles")
            .Produces<PerformanceSummaryDto>();

        return app;
    }

    private static async Task<IResult> GetUsage(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? model,
        [FromQuery] string? sourceModule,
        [FromQuery] Guid? projectId,
        GetUsageHandler handler,
        CancellationToken ct)
    {
        var query = new GetUsageQuery(from, to, model, sourceModule, projectId);
        Result<UsageSummaryDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetPerformance(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? model,
        GetPerformanceHandler handler,
        CancellationToken ct)
    {
        var query = new GetPerformanceQuery(from, to, model);
        Result<PerformanceSummaryDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }
}
