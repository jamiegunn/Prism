using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Prism.Common.Results;
using Prism.Features.Notebooks.Api.Requests;
using Prism.Features.Notebooks.Application.CreateNotebook;
using Prism.Features.Notebooks.Application.DeleteNotebook;
using Prism.Features.Notebooks.Application.Dtos;
using Prism.Features.Notebooks.Application.GetNotebook;
using Prism.Features.Notebooks.Application.ListNotebooks;
using Prism.Features.Notebooks.Application.UpdateNotebook;

namespace Prism.Features.Notebooks.Api;

/// <summary>
/// Defines the HTTP endpoints for the Notebooks feature including CRUD operations
/// and .ipynb content download.
/// </summary>
public static class NotebookEndpoints
{
    /// <summary>
    /// Maps the notebook endpoints under <c>/api/v1/notebooks</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group builder for further configuration.</returns>
    public static RouteGroupBuilder MapNotebookEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/notebooks")
            .WithTags("Notebooks");

        group.MapPost("/", CreateNotebook)
            .WithName("CreateNotebook")
            .WithSummary("Creates a new notebook with default .ipynb structure")
            .Produces<NotebookSummaryDto>(StatusCodes.Status200OK);

        group.MapGet("/", ListNotebooks)
            .WithName("ListNotebooks")
            .WithSummary("Lists all notebooks")
            .Produces<List<NotebookSummaryDto>>();

        group.MapGet("/{id:guid}", GetNotebook)
            .WithName("GetNotebook")
            .WithSummary("Gets a notebook with full .ipynb content")
            .Produces<NotebookDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateNotebook)
            .WithName("UpdateNotebook")
            .WithSummary("Updates a notebook content and/or metadata")
            .Produces<NotebookDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteNotebook)
            .WithName("DeleteNotebook")
            .WithSummary("Deletes a notebook")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/download", DownloadNotebook)
            .WithName("DownloadNotebook")
            .WithSummary("Downloads a notebook as .ipynb file")
            .Produces(StatusCodes.Status200OK, contentType: "application/x-ipynb+json")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> CreateNotebook(
        [FromBody] CreateNotebookRequest request,
        CreateNotebookHandler handler,
        CancellationToken ct)
    {
        var command = new CreateNotebookCommand(request.Name, request.Description, request.Content);
        Result<NotebookSummaryDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListNotebooks(
        [FromQuery] string? search,
        ListNotebooksHandler handler,
        CancellationToken ct)
    {
        var query = new ListNotebooksQuery(search);
        Result<List<NotebookSummaryDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            list => TypedResults.Ok(list),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetNotebook(
        Guid id,
        GetNotebookHandler handler,
        CancellationToken ct)
    {
        var query = new GetNotebookQuery(id);
        Result<NotebookDetailDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> UpdateNotebook(
        Guid id,
        [FromBody] UpdateNotebookRequest request,
        UpdateNotebookHandler handler,
        CancellationToken ct)
    {
        var command = new UpdateNotebookCommand(id, request.Name, request.Description, request.Content);
        Result<NotebookDetailDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> DeleteNotebook(
        Guid id,
        DeleteNotebookHandler handler,
        CancellationToken ct)
    {
        var command = new DeleteNotebookCommand(id);
        Result result = await handler.HandleAsync(command, ct);

        return result.ToHttpResult();
    }

    private static async Task<IResult> DownloadNotebook(
        Guid id,
        GetNotebookHandler handler,
        CancellationToken ct)
    {
        var query = new GetNotebookQuery(id);
        Result<NotebookDetailDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto =>
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(dto.Content);
                string filename = $"{dto.Name.Replace(' ', '_')}.ipynb";
                return TypedResults.File(bytes, "application/x-ipynb+json", filename);
            },
            error => error.ToHttpResult());
    }
}
