using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Prism.Common.Results;
using Prism.Features.Workspaces.Api.Requests;
using Prism.Features.Workspaces.Application.CreateWorkspace;
using Prism.Features.Workspaces.Application.Dtos;
using Prism.Features.Workspaces.Application.GetWorkspace;
using Prism.Features.Workspaces.Application.ListWorkspaces;

namespace Prism.Features.Workspaces.Api;

/// <summary>
/// Defines the HTTP endpoints for workspace management.
/// </summary>
public static class WorkspaceEndpoints
{
    /// <summary>
    /// Maps the workspace endpoints under <c>/api/v1/workspaces</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group builder.</returns>
    public static RouteGroupBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/workspaces")
            .WithTags("Workspaces");

        group.MapPost("/", CreateWorkspace)
            .WithName("CreateWorkspace")
            .WithSummary("Creates a new workspace")
            .Produces<WorkspaceDto>(StatusCodes.Status201Created);

        group.MapGet("/", ListWorkspaces)
            .WithName("ListWorkspaces")
            .WithSummary("Lists all workspaces")
            .Produces<List<WorkspaceDto>>();

        group.MapGet("/{id:guid}", GetWorkspace)
            .WithName("GetWorkspace")
            .WithSummary("Gets a workspace by ID")
            .Produces<WorkspaceDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> CreateWorkspace(
        [FromBody] CreateWorkspaceRequest request,
        CreateWorkspaceHandler handler,
        CancellationToken ct)
    {
        var command = new CreateWorkspaceCommand(request.Name, request.Description, request.IconColor);
        Result<WorkspaceDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Created($"/api/v1/workspaces/{dto.Id}", dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListWorkspaces(
        ListWorkspacesHandler handler,
        CancellationToken ct)
    {
        Result<List<WorkspaceDto>> result = await handler.HandleAsync(ct);

        return result.Match(
            dtos => TypedResults.Ok(dtos),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetWorkspace(
        Guid id,
        GetWorkspaceHandler handler,
        CancellationToken ct)
    {
        Result<WorkspaceDto> result = await handler.HandleAsync(id, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }
}
