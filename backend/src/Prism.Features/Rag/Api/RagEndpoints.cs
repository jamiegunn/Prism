using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Prism.Common.Results;
using Prism.Features.Rag.Api.Requests;
using Prism.Features.Rag.Application.CreateCollection;
using Prism.Features.Rag.Application.DeleteCollection;
using Prism.Features.Rag.Application.Dtos;
using Prism.Features.Rag.Application.GetCollection;
using Prism.Features.Rag.Application.GetCollectionStats;
using Prism.Features.Rag.Application.IngestDocument;
using Prism.Features.Rag.Application.ListCollections;
using Prism.Features.Rag.Application.ListDocuments;
using Prism.Features.Rag.Application.QueryCollection;
using Prism.Features.Rag.Application.RagPipeline;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Api;

/// <summary>
/// Defines the HTTP endpoints for the RAG Workbench.
/// </summary>
public static class RagEndpoints
{
    /// <summary>
    /// Maps the RAG endpoints under <c>/api/v1/rag</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapRagEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder rag = app.MapGroup("/api/v1/rag")
            .WithTags("RAG");

        // Collection CRUD
        rag.MapPost("/collections", CreateCollection)
            .WithName("CreateRagCollection")
            .WithSummary("Creates a new RAG collection")
            .Produces<RagCollectionDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        rag.MapGet("/collections", ListCollections)
            .WithName("ListRagCollections")
            .WithSummary("Lists all RAG collections")
            .Produces<List<RagCollectionDto>>();

        rag.MapGet("/collections/{id:guid}", GetCollection)
            .WithName("GetRagCollection")
            .WithSummary("Gets a specific RAG collection")
            .Produces<RagCollectionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        rag.MapDelete("/collections/{id:guid}", DeleteCollection)
            .WithName("DeleteRagCollection")
            .WithSummary("Deletes a RAG collection and all its data")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Document management
        rag.MapPost("/collections/{id:guid}/ingest", IngestDocument)
            .WithName("IngestDocument")
            .WithSummary("Ingests a document into a collection (parses, chunks, embeds)")
            .Produces<RagDocumentDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .DisableAntiforgery();

        rag.MapGet("/collections/{id:guid}/documents", ListDocuments)
            .WithName("ListRagDocuments")
            .WithSummary("Lists documents in a collection")
            .Produces<List<RagDocumentDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Search & RAG
        rag.MapPost("/collections/{id:guid}/query", QueryCollection)
            .WithName("QueryRagCollection")
            .WithSummary("Searches a collection using vector, BM25, or hybrid search")
            .Produces<List<ChunkSearchResultDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        rag.MapPost("/collections/{id:guid}/rag", ExecuteRagPipeline)
            .WithName("ExecuteRagPipeline")
            .WithSummary("Executes the full RAG pipeline: retrieve + generate")
            .Produces<RagPipelineResultDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Stats
        rag.MapGet("/collections/{id:guid}/stats", GetCollectionStats)
            .WithName("GetRagCollectionStats")
            .WithSummary("Gets statistics for a RAG collection")
            .Produces<CollectionStatsDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateCollection(
        [FromBody] CreateCollectionRequest request,
        CreateCollectionHandler handler,
        CancellationToken ct)
    {
        var command = new CreateCollectionCommand(
            request.Name, request.Description, request.EmbeddingModel,
            request.Dimensions, request.DistanceMetric, request.ChunkingStrategy,
            request.ChunkSize, request.ChunkOverlap, request.ProjectId);

        Result<RagCollectionDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Created($"/api/v1/rag/collections/{dto.Id}", dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListCollections(
        [FromQuery] Guid? projectId,
        [FromQuery] string? search,
        ListCollectionsHandler handler,
        CancellationToken ct)
    {
        var query = new ListCollectionsQuery(projectId, search);
        Result<List<RagCollectionDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dtos => TypedResults.Ok(dtos),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetCollection(
        Guid id,
        GetCollectionHandler handler,
        CancellationToken ct)
    {
        var query = new GetCollectionQuery(id);
        Result<RagCollectionDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> DeleteCollection(
        Guid id,
        DeleteCollectionHandler handler,
        CancellationToken ct)
    {
        var command = new DeleteCollectionCommand(id);
        Result result = await handler.HandleAsync(command, ct);

        return result.ToHttpResult();
    }

    private static async Task<IResult> IngestDocument(
        Guid id,
        HttpRequest request,
        IngestDocumentHandler handler,
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

        var command = new IngestDocumentCommand(
            id, file.FileName, stream, file.Length, file.ContentType);

        Result<RagDocumentDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Created($"/api/v1/rag/collections/{id}/documents/{dto.Id}", dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListDocuments(
        Guid id,
        ListDocumentsHandler handler,
        CancellationToken ct)
    {
        var query = new ListDocumentsQuery(id);
        Result<List<RagDocumentDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dtos => TypedResults.Ok(dtos),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> QueryCollection(
        Guid id,
        [FromBody] QueryCollectionRequest request,
        QueryCollectionHandler handler,
        CancellationToken ct)
    {
        SearchType searchType = Enum.TryParse<SearchType>(request.SearchType, true, out SearchType parsed)
            ? parsed
            : SearchType.Vector;

        var query = new QueryCollectionQuery(id, request.QueryText, request.TopK, searchType, request.VectorWeight);
        Result<List<ChunkSearchResultDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dtos => TypedResults.Ok(dtos),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ExecuteRagPipeline(
        Guid id,
        [FromBody] RagPipelineRequest request,
        RagPipelineHandler handler,
        CancellationToken ct)
    {
        SearchType searchType = Enum.TryParse<SearchType>(request.SearchType, true, out SearchType parsed)
            ? parsed
            : SearchType.Vector;

        var command = new RagPipelineCommand(
            id, request.Query, request.Model, request.InstanceId,
            request.SystemPrompt, request.PromptTemplate, request.TopK,
            searchType, request.Temperature, request.MaxTokens);

        Result<RagPipelineResultDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetCollectionStats(
        Guid id,
        GetCollectionStatsHandler handler,
        CancellationToken ct)
    {
        var query = new GetCollectionStatsQuery(id);
        Result<CollectionStatsDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }
}
