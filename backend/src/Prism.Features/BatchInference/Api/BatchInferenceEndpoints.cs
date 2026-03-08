using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Prism.Common.Abstractions;
using Prism.Common.Results;
using Prism.Features.BatchInference.Api.Requests;
using Prism.Features.BatchInference.Application.CreateBatchJob;
using Prism.Features.BatchInference.Application.DownloadBatchResults;
using Prism.Features.BatchInference.Application.Dtos;
using Prism.Features.BatchInference.Application.EstimateBatchCost;
using Prism.Features.BatchInference.Application.GetBatchJob;
using Prism.Features.BatchInference.Application.GetBatchResults;
using Prism.Features.BatchInference.Application.ListBatchJobs;
using Prism.Features.BatchInference.Application.RetryFailed;
using Prism.Features.BatchInference.Application.UpdateBatchJobStatus;

namespace Prism.Features.BatchInference.Api;

/// <summary>
/// Defines the HTTP endpoints for batch inference jobs.
/// </summary>
public static class BatchInferenceEndpoints
{
    /// <summary>
    /// Maps the batch inference endpoints under <c>/api/v1/batch</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapBatchInferenceEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/batch")
            .WithTags("Batch Inference");

        group.MapPost("/", CreateBatchJob)
            .WithName("CreateBatchJob")
            .WithSummary("Creates a new batch inference job")
            .Produces<BatchJobDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/", ListBatchJobs)
            .WithName("ListBatchJobs")
            .WithSummary("Lists all batch jobs with optional status filter")
            .Produces<List<BatchJobDto>>();

        group.MapGet("/{id:guid}", GetBatchJob)
            .WithName("GetBatchJob")
            .WithSummary("Gets a specific batch job by ID")
            .Produces<BatchJobDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/pause", PauseBatchJob)
            .WithName("PauseBatchJob")
            .WithSummary("Pauses a running batch job")
            .Produces<BatchJobDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/resume", ResumeBatchJob)
            .WithName("ResumeBatchJob")
            .WithSummary("Resumes a paused batch job")
            .Produces<BatchJobDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/cancel", CancelBatchJob)
            .WithName("CancelBatchJob")
            .WithSummary("Cancels a batch job")
            .Produces<BatchJobDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/results", GetBatchResults)
            .WithName("GetBatchResults")
            .WithSummary("Gets paginated batch results")
            .Produces<PagedResult<BatchResultDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/download", DownloadBatchResults)
            .WithName("DownloadBatchResults")
            .WithSummary("Downloads batch results as CSV, JSON, or JSONL")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/retry-failed", RetryFailed)
            .WithName("RetryFailedBatchRecords")
            .WithSummary("Retries all failed records in a batch job")
            .Produces<BatchJobDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/estimate", EstimateBatchCost)
            .WithName("EstimateBatchCost")
            .WithSummary("Estimates cost and time for a batch job")
            .Produces<BatchEstimateDto>()
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> CreateBatchJob(
        [FromBody] CreateBatchJobRequest request,
        CreateBatchJobHandler handler,
        CancellationToken ct)
    {
        var command = new CreateBatchJobCommand(
            request.DatasetId, request.SplitLabel, request.Model, request.PromptVersionId,
            request.Parameters, request.Concurrency, request.MaxRetries, request.CaptureLogprobs);

        Result<BatchJobDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Created($"/api/v1/batch/{dto.Id}", dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListBatchJobs(
        [FromQuery] string? status,
        ListBatchJobsHandler handler,
        CancellationToken ct)
    {
        var query = new ListBatchJobsQuery(status);
        Result<List<BatchJobDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dtos => TypedResults.Ok(dtos),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetBatchJob(
        Guid id,
        GetBatchJobHandler handler,
        CancellationToken ct)
    {
        var query = new GetBatchJobQuery(id);
        Result<BatchJobDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> PauseBatchJob(
        Guid id,
        UpdateBatchJobStatusHandler handler,
        CancellationToken ct)
    {
        var command = new UpdateBatchJobStatusCommand(id, "pause");
        Result<BatchJobDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ResumeBatchJob(
        Guid id,
        UpdateBatchJobStatusHandler handler,
        CancellationToken ct)
    {
        var command = new UpdateBatchJobStatusCommand(id, "resume");
        Result<BatchJobDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> CancelBatchJob(
        Guid id,
        UpdateBatchJobStatusHandler handler,
        CancellationToken ct)
    {
        var command = new UpdateBatchJobStatusCommand(id, "cancel");
        Result<BatchJobDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetBatchResults(
        Guid id,
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        GetBatchResultsHandler handler,
        CancellationToken ct)
    {
        var query = new GetBatchResultsQuery(id, status, page ?? 1, pageSize ?? 50);
        Result<PagedResult<BatchResultDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> DownloadBatchResults(
        Guid id,
        [FromQuery] string format,
        DownloadBatchResultsHandler handler,
        CancellationToken ct)
    {
        var query = new DownloadBatchResultsQuery(id, format ?? "json");
        Result<DownloadResultData> result = await handler.HandleAsync(query, ct);

        return result.Match(
            data => TypedResults.File(data.Data, data.ContentType, data.FileName),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> RetryFailed(
        Guid id,
        RetryFailedHandler handler,
        CancellationToken ct)
    {
        var command = new RetryFailedCommand(id);
        Result<BatchJobDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> EstimateBatchCost(
        [FromBody] EstimateBatchCostRequest request,
        EstimateBatchCostHandler handler,
        CancellationToken ct)
    {
        var command = new EstimateBatchCostCommand(request.DatasetId, request.SplitLabel, request.Model, request.Concurrency);
        Result<BatchEstimateDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }
}
