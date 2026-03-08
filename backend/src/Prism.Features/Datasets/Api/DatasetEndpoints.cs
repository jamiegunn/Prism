using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Prism.Common.Abstractions;
using Prism.Common.Results;
using Prism.Features.Datasets.Api.Requests;
using Prism.Features.Datasets.Application.DeleteDataset;
using Prism.Features.Datasets.Application.Dtos;
using Prism.Features.Datasets.Application.ExportDataset;
using Prism.Features.Datasets.Application.GetDataset;
using Prism.Features.Datasets.Application.GetDatasetStats;
using Prism.Features.Datasets.Application.ListDatasets;
using Prism.Features.Datasets.Application.ListRecords;
using Prism.Features.Datasets.Application.SplitDataset;
using Prism.Features.Datasets.Application.UpdateDataset;
using Prism.Features.Datasets.Application.UpdateRecord;
using Prism.Features.Datasets.Application.UploadDataset;

namespace Prism.Features.Datasets.Api;

/// <summary>
/// Defines the HTTP endpoints for dataset management.
/// </summary>
public static class DatasetEndpoints
{
    /// <summary>
    /// Maps the dataset endpoints under <c>/api/v1/datasets</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapDatasetEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder datasets = app.MapGroup("/api/v1/datasets")
            .WithTags("Datasets");

        datasets.MapPost("/", UploadDataset)
            .WithName("UploadDataset")
            .WithSummary("Uploads and parses a dataset file (CSV, JSON, JSONL)")
            .Produces<DatasetDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .DisableAntiforgery();

        datasets.MapGet("/", ListDatasets)
            .WithName("ListDatasets")
            .WithSummary("Lists all datasets with optional filters")
            .Produces<List<DatasetDto>>();

        datasets.MapGet("/{id:guid}", GetDataset)
            .WithName("GetDataset")
            .WithSummary("Gets a specific dataset by ID")
            .Produces<DatasetDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        datasets.MapPut("/{id:guid}", UpdateDataset)
            .WithName("UpdateDataset")
            .WithSummary("Updates dataset metadata and schema")
            .Produces<DatasetDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        datasets.MapDelete("/{id:guid}", DeleteDataset)
            .WithName("DeleteDataset")
            .WithSummary("Deletes a dataset and all its records")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        datasets.MapGet("/{id:guid}/records", ListRecords)
            .WithName("ListDatasetRecords")
            .WithSummary("Lists records in a dataset with pagination")
            .Produces<PagedResult<DatasetRecordDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        datasets.MapPut("/{id:guid}/records/{recordId:guid}", UpdateRecord)
            .WithName("UpdateDatasetRecord")
            .WithSummary("Updates a single record's data")
            .Produces<DatasetRecordDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        datasets.MapPost("/{id:guid}/split", SplitDataset)
            .WithName("SplitDataset")
            .WithSummary("Splits the dataset into train/test/val partitions")
            .Produces<DatasetDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        datasets.MapGet("/{id:guid}/stats", GetDatasetStats)
            .WithName("GetDatasetStats")
            .WithSummary("Gets statistics for a dataset")
            .Produces<DatasetStatsDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        datasets.MapPost("/{id:guid}/export", ExportDataset)
            .WithName("ExportDataset")
            .WithSummary("Exports dataset records in CSV, JSON, or JSONL format")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> UploadDataset(
        HttpRequest request,
        [FromQuery] string name,
        [FromQuery] string? description,
        [FromQuery] Guid? projectId,
        UploadDatasetHandler handler,
        CancellationToken ct)
    {
        if (!request.HasFormContentType || request.Form.Files.Count == 0)
        {
            return TypedResults.Problem(
                detail: "A file must be uploaded.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        IFormFile file = request.Form.Files[0];
        using Stream stream = file.OpenReadStream();

        var command = new UploadDatasetCommand(
            file.FileName, stream, file.Length, name, description, projectId);

        Result<DatasetDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Created($"/api/v1/datasets/{dto.Id}", dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListDatasets(
        [FromQuery] Guid? projectId,
        [FromQuery] string? search,
        ListDatasetsHandler handler,
        CancellationToken ct)
    {
        var query = new ListDatasetsQuery(projectId, search);
        Result<List<DatasetDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dtos => TypedResults.Ok(dtos),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetDataset(
        Guid id,
        GetDatasetHandler handler,
        CancellationToken ct)
    {
        var query = new GetDatasetQuery(id);
        Result<DatasetDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> UpdateDataset(
        Guid id,
        [FromBody] UpdateDatasetRequest request,
        UpdateDatasetHandler handler,
        CancellationToken ct)
    {
        var command = new UpdateDatasetCommand(id, request.Name, request.Description, request.Schema);
        Result<DatasetDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> DeleteDataset(
        Guid id,
        DeleteDatasetHandler handler,
        CancellationToken ct)
    {
        var command = new DeleteDatasetCommand(id);
        Result result = await handler.HandleAsync(command, ct);

        return result.ToHttpResult();
    }

    private static async Task<IResult> ListRecords(
        Guid id,
        [FromQuery] string? splitLabel,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        ListRecordsHandler handler,
        CancellationToken ct)
    {
        var query = new ListRecordsQuery(id, splitLabel, page ?? 1, pageSize ?? 50);
        Result<PagedResult<DatasetRecordDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> UpdateRecord(
        Guid id,
        Guid recordId,
        [FromBody] UpdateRecordRequest request,
        UpdateRecordHandler handler,
        CancellationToken ct)
    {
        var command = new UpdateRecordCommand(id, recordId, request.Data);
        Result<DatasetRecordDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> SplitDataset(
        Guid id,
        [FromBody] SplitDatasetRequest request,
        SplitDatasetHandler handler,
        CancellationToken ct)
    {
        var command = new SplitDatasetCommand(id, request.TrainRatio, request.TestRatio, request.ValRatio, request.Seed);
        Result<DatasetDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetDatasetStats(
        Guid id,
        GetDatasetStatsHandler handler,
        CancellationToken ct)
    {
        var query = new GetDatasetStatsQuery(id);
        Result<DatasetStatsDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ExportDataset(
        Guid id,
        [FromQuery] string format,
        [FromQuery] string? splitLabel,
        ExportDatasetHandler handler,
        CancellationToken ct)
    {
        var query = new ExportDatasetQuery(id, format ?? "json", splitLabel);
        Result<ExportResult> result = await handler.HandleAsync(query, ct);

        return result.Match(
            export => TypedResults.File(export.Data, export.ContentType, export.FileName),
            error => error.ToHttpResult());
    }
}
