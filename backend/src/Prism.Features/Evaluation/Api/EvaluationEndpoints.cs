using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Prism.Common.Abstractions;
using Prism.Common.Results;
using Prism.Features.Evaluation.Api.Requests;
using Prism.Features.Evaluation.Application.CancelEvaluation;
using Prism.Features.Evaluation.Application.Dtos;
using Prism.Features.Evaluation.Application.ExportResults;
using Prism.Features.Evaluation.Application.GetEvaluation;
using Prism.Features.Evaluation.Application.GetEvaluationResults;
using Prism.Features.Evaluation.Application.GetLeaderboard;
using Prism.Features.Evaluation.Application.GetResultRecords;
using Prism.Features.Evaluation.Application.ListEvaluations;
using Prism.Features.Evaluation.Application.StartEvaluation;

namespace Prism.Features.Evaluation.Api;

/// <summary>
/// Defines the HTTP endpoints for evaluation management.
/// </summary>
public static class EvaluationEndpoints
{
    /// <summary>
    /// Maps the evaluation endpoints under <c>/api/v1/evaluation</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapEvaluationEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/evaluation")
            .WithTags("Evaluation");

        group.MapPost("/", StartEvaluation)
            .WithName("StartEvaluation")
            .WithSummary("Starts a new evaluation against a dataset")
            .Produces<EvaluationDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/", ListEvaluations)
            .WithName("ListEvaluations")
            .WithSummary("Lists all evaluations with optional filters")
            .Produces<List<EvaluationDto>>();

        group.MapGet("/{id:guid}", GetEvaluation)
            .WithName("GetEvaluation")
            .WithSummary("Gets a specific evaluation by ID")
            .Produces<EvaluationDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/cancel", CancelEvaluation)
            .WithName("CancelEvaluation")
            .WithSummary("Cancels a running or pending evaluation")
            .Produces<EvaluationDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/results", GetEvaluationResults)
            .WithName("GetEvaluationResults")
            .WithSummary("Gets aggregated evaluation results by model")
            .Produces<EvaluationSummaryDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/results/records", GetResultRecords)
            .WithName("GetResultRecords")
            .WithSummary("Gets individual evaluation result records with pagination")
            .Produces<PagedResult<EvaluationResultDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/results/export", ExportResults)
            .WithName("ExportEvaluationResults")
            .WithSummary("Exports evaluation results in CSV or JSON format")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/leaderboard", GetLeaderboard)
            .WithName("GetLeaderboard")
            .WithSummary("Gets a leaderboard of model performances across evaluations")
            .Produces<List<LeaderboardEntryDto>>();

        return app;
    }

    private static async Task<IResult> StartEvaluation(
        [FromBody] StartEvaluationRequest request,
        StartEvaluationHandler handler,
        CancellationToken ct)
    {
        var command = new StartEvaluationCommand(
            request.Name, request.DatasetId, request.SplitLabel, request.ProjectId,
            request.Models, request.PromptVersionId, request.ScoringMethods, request.Config);

        Result<EvaluationDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Created($"/api/v1/evaluation/{dto.Id}", dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListEvaluations(
        [FromQuery] Guid? projectId,
        [FromQuery] string? search,
        ListEvaluationsHandler handler,
        CancellationToken ct)
    {
        var query = new ListEvaluationsQuery(projectId, search);
        Result<List<EvaluationDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dtos => TypedResults.Ok(dtos),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetEvaluation(
        Guid id,
        GetEvaluationHandler handler,
        CancellationToken ct)
    {
        var query = new GetEvaluationQuery(id);
        Result<EvaluationDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> CancelEvaluation(
        Guid id,
        CancelEvaluationHandler handler,
        CancellationToken ct)
    {
        var command = new CancelEvaluationCommand(id);
        Result<EvaluationDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetEvaluationResults(
        Guid id,
        GetEvaluationResultsHandler handler,
        CancellationToken ct)
    {
        var query = new GetEvaluationResultsQuery(id);
        Result<EvaluationSummaryDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetResultRecords(
        Guid id,
        [FromQuery] string? model,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        GetResultRecordsHandler handler,
        CancellationToken ct)
    {
        var query = new GetResultRecordsQuery(id, model, page ?? 1, pageSize ?? 50);
        Result<PagedResult<EvaluationResultDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ExportResults(
        Guid id,
        [FromQuery] string format,
        [FromQuery] string? model,
        ExportResultsHandler handler,
        CancellationToken ct)
    {
        var query = new ExportResultsQuery(id, format ?? "json", model);
        Result<ExportResultsData> result = await handler.HandleAsync(query, ct);

        return result.Match(
            export => TypedResults.File(export.Data, export.ContentType, export.FileName),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetLeaderboard(
        [FromQuery] Guid? projectId,
        [FromQuery] string? scoringMethod,
        GetLeaderboardHandler handler,
        CancellationToken ct)
    {
        var query = new GetLeaderboardQuery(projectId, scoringMethod);
        Result<List<LeaderboardEntryDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dtos => TypedResults.Ok(dtos),
            error => error.ToHttpResult());
    }
}
