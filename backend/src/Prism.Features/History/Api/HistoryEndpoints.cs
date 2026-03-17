using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Prism.Common.Abstractions;
using Prism.Common.Results;
using Prism.Features.History.Api.Requests;
using Prism.Features.History.Application.Dtos;
using Prism.Features.History.Application.GetRecord;
using Prism.Features.History.Application.ReplaySingle;
using Prism.Features.History.Application.SearchHistory;
using Prism.Features.History.Application.TagRecord;

namespace Prism.Features.History.Api;

/// <summary>
/// Defines the HTTP endpoints for the History and Replay feature including
/// searching, viewing, tagging, and replaying inference records.
/// </summary>
public static class HistoryEndpoints
{
    /// <summary>
    /// Maps the history endpoints under <c>/api/v1/history</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder (typically the WebApplication).</param>
    /// <returns>The route group builder for further configuration.</returns>
    public static RouteGroupBuilder MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/history")
            .WithTags("History");

        group.MapGet("/", SearchHistory)
            .WithName("SearchHistory")
            .WithSummary("Searches inference history with filters and pagination")
            .Produces<PagedResult<InferenceRecordSummaryDto>>();

        group.MapGet("/{id:guid}", GetRecord)
            .WithName("GetHistoryRecord")
            .WithSummary("Gets a single inference record by ID with full detail")
            .Produces<InferenceRecordDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/tags", TagRecord)
            .WithName("TagHistoryRecord")
            .WithSummary("Replaces the tags on an inference record")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/replay", ReplaySingle)
            .WithName("ReplayHistoryRecord")
            .WithSummary("Replays an inference record against a specified provider instance")
            .Produces<ReplayResultDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    /// <summary>
    /// Handles the search history endpoint with query parameter filters.
    /// </summary>
    private static async Task<IResult> SearchHistory(
        [FromQuery] string? search,
        [FromQuery] string? sourceModule,
        [FromQuery] string? model,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? tags,
        [FromQuery] bool? isSuccess,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        SearchHistoryHandler handler,
        CancellationToken ct)
    {
        List<string>? tagList = !string.IsNullOrWhiteSpace(tags)
            ? tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : null;

        var query = new SearchHistoryQuery(
            search, sourceModule, model, from, to, tagList, isSuccess,
            page ?? 1, pageSize ?? 20);

        Result<PagedResult<InferenceRecordSummaryDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            pagedResult => TypedResults.Ok(pagedResult),
            error => error.ToHttpResult());
    }

    /// <summary>
    /// Handles the get record endpoint.
    /// </summary>
    private static async Task<IResult> GetRecord(
        Guid id,
        GetRecordHandler handler,
        CancellationToken ct)
    {
        var query = new GetRecordQuery(id);
        Result<InferenceRecordDetailDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    /// <summary>
    /// Handles the tag record endpoint.
    /// </summary>
    private static async Task<IResult> TagRecord(
        Guid id,
        [FromBody] TagRecordRequest request,
        TagRecordHandler handler,
        CancellationToken ct)
    {
        var command = new TagRecordCommand(id, request.Tags);
        Result result = await handler.HandleAsync(command, ct);

        return result.ToHttpResult();
    }

    /// <summary>
    /// Handles the replay single endpoint.
    /// </summary>
    private static async Task<IResult> ReplaySingle(
        Guid id,
        [FromBody] ReplaySingleRequest request,
        ReplaySingleHandler handler,
        CancellationToken ct)
    {
        var command = new ReplaySingleCommand(
            id,
            request.InstanceId,
            request.OverrideModel,
            request.OverrideTemperature,
            request.OverrideMaxTokens,
            request.OverrideTopP);
        Result<ReplayResultDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }
}
