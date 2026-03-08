using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Prism.Common.Results;
using Prism.Features.Experiments.Api.Requests;
using Prism.Common.Abstractions;
using Prism.Features.Experiments.Application.ArchiveExperiment;
using Prism.Features.Experiments.Application.ArchiveProject;
using Prism.Features.Experiments.Application.CompareRuns;
using Prism.Features.Experiments.Application.CreateExperiment;
using Prism.Features.Experiments.Application.CreateProject;
using Prism.Features.Experiments.Application.CreateRun;
using Prism.Features.Experiments.Application.DeleteRun;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Application.ExportRuns;
using Prism.Features.Experiments.Application.GetExperiment;
using Prism.Features.Experiments.Application.GetProject;
using Prism.Features.Experiments.Application.GetRun;
using Prism.Features.Experiments.Application.ListExperiments;
using Prism.Features.Experiments.Application.ListProjects;
using Prism.Features.Experiments.Application.ListRuns;
using Prism.Features.Experiments.Application.UpdateExperiment;
using Prism.Features.Experiments.Application.UpdateProject;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Api;

/// <summary>
/// Defines the HTTP endpoints for managing research projects and experiments.
/// </summary>
public static class ExperimentEndpoints
{
    /// <summary>
    /// Maps the project and experiment endpoints under <c>/api/v1/projects</c> and <c>/api/v1/experiments</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The web application for chaining.</returns>
    public static IEndpointRouteBuilder MapExperimentEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder projects = app.MapGroup("/api/v1/projects")
            .WithTags("Projects");

        projects.MapPost("/", CreateProject)
            .WithName("CreateProject")
            .WithSummary("Creates a new research project")
            .Produces<ProjectDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        projects.MapGet("/", ListProjects)
            .WithName("ListProjects")
            .WithSummary("Lists all research projects")
            .Produces<List<ProjectDto>>();

        projects.MapGet("/{id:guid}", GetProject)
            .WithName("GetProject")
            .WithSummary("Gets a specific project by ID")
            .Produces<ProjectDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects.MapPut("/{id:guid}", UpdateProject)
            .WithName("UpdateProject")
            .WithSummary("Updates a research project")
            .Produces<ProjectDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects.MapPost("/{id:guid}/archive", ArchiveProject)
            .WithName("ArchiveProject")
            .WithSummary("Archives a research project")
            .Produces<ProjectDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        RouteGroupBuilder experiments = app.MapGroup("/api/v1/experiments")
            .WithTags("Experiments");

        experiments.MapPost("/", CreateExperiment)
            .WithName("CreateExperiment")
            .WithSummary("Creates a new experiment within a project")
            .Produces<ExperimentDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        experiments.MapGet("/", ListExperiments)
            .WithName("ListExperiments")
            .WithSummary("Lists experiments with optional project and status filters")
            .Produces<List<ExperimentDto>>();

        experiments.MapGet("/{id:guid}", GetExperiment)
            .WithName("GetExperiment")
            .WithSummary("Gets a specific experiment by ID")
            .Produces<ExperimentDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        experiments.MapPut("/{id:guid}", UpdateExperiment)
            .WithName("UpdateExperiment")
            .WithSummary("Updates an experiment")
            .Produces<ExperimentDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        experiments.MapPost("/{id:guid}/status", ChangeExperimentStatus)
            .WithName("ChangeExperimentStatus")
            .WithSummary("Changes the status of an experiment (Active, Completed, Archived)")
            .Produces<ExperimentDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        // Run endpoints
        experiments.MapPost("/{id:guid}/runs", CreateRun)
            .WithName("CreateRun")
            .WithSummary("Creates a new run in an experiment")
            .Produces<RunDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound);

        experiments.MapGet("/{id:guid}/runs", ListRuns)
            .WithName("ListRuns")
            .WithSummary("Lists runs in an experiment with filtering, sorting, and pagination")
            .Produces<PagedResult<RunDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        experiments.MapGet("/{id:guid}/runs/{runId:guid}", GetRun)
            .WithName("GetRun")
            .WithSummary("Gets a specific run by ID")
            .Produces<RunDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        experiments.MapDelete("/{id:guid}/runs/{runId:guid}", DeleteRun)
            .WithName("DeleteRun")
            .WithSummary("Deletes a run from an experiment")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        experiments.MapPost("/{id:guid}/compare", CompareRuns)
            .WithName("CompareRuns")
            .WithSummary("Compares multiple runs side-by-side with parameter diffs and metric deltas")
            .Produces<RunComparisonDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        experiments.MapGet("/{id:guid}/runs/export", ExportRuns)
            .WithName("ExportRuns")
            .WithSummary("Exports runs from an experiment in CSV or JSON format")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateProject(
        [FromBody] CreateProjectRequest request,
        CreateProjectHandler handler,
        CancellationToken ct)
    {
        var command = new CreateProjectCommand(request.Name, request.Description);
        Result<ProjectDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Created($"/api/v1/projects/{dto.Id}", dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListProjects(
        [FromQuery] bool? includeArchived,
        [FromQuery] string? search,
        ListProjectsHandler handler,
        CancellationToken ct)
    {
        var query = new ListProjectsQuery(includeArchived ?? false, search);
        Result<List<ProjectDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dtos => TypedResults.Ok(dtos),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetProject(
        Guid id,
        GetProjectHandler handler,
        CancellationToken ct)
    {
        var query = new GetProjectQuery(id);
        Result<ProjectDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> UpdateProject(
        Guid id,
        [FromBody] UpdateProjectRequest request,
        UpdateProjectHandler handler,
        CancellationToken ct)
    {
        var command = new UpdateProjectCommand(id, request.Name, request.Description);
        Result<ProjectDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ArchiveProject(
        Guid id,
        ArchiveProjectHandler handler,
        CancellationToken ct)
    {
        var command = new ArchiveProjectCommand(id);
        Result<ProjectDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> CreateExperiment(
        [FromBody] CreateExperimentRequest request,
        CreateExperimentHandler handler,
        CancellationToken ct)
    {
        var command = new CreateExperimentCommand(
            request.ProjectId, request.Name, request.Description, request.Hypothesis);
        Result<ExperimentDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Created($"/api/v1/experiments/{dto.Id}", dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListExperiments(
        [FromQuery] Guid? projectId,
        [FromQuery] string? status,
        ListExperimentsHandler handler,
        CancellationToken ct)
    {
        ExperimentStatus? statusFilter = null;
        if (status is not null && Enum.TryParse<ExperimentStatus>(status, ignoreCase: true, out ExperimentStatus parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var query = new ListExperimentsQuery(projectId, statusFilter);
        Result<List<ExperimentDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dtos => TypedResults.Ok(dtos),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetExperiment(
        Guid id,
        GetExperimentHandler handler,
        CancellationToken ct)
    {
        var query = new GetExperimentQuery(id);
        Result<ExperimentDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> UpdateExperiment(
        Guid id,
        [FromBody] UpdateExperimentRequest request,
        UpdateExperimentHandler handler,
        CancellationToken ct)
    {
        var command = new UpdateExperimentCommand(id, request.Name, request.Description, request.Hypothesis);
        Result<ExperimentDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ChangeExperimentStatus(
        Guid id,
        [FromBody] ChangeExperimentStatusRequest request,
        ArchiveExperimentHandler handler,
        CancellationToken ct)
    {
        if (!Enum.TryParse<ExperimentStatus>(request.Status, ignoreCase: true, out ExperimentStatus newStatus))
        {
            return TypedResults.Problem(
                detail: $"Invalid status '{request.Status}'. Valid values: {string.Join(", ", Enum.GetNames<ExperimentStatus>())}",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var command = new ArchiveExperimentCommand(id, newStatus);
        Result<ExperimentDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> CreateRun(
        Guid id,
        [FromBody] CreateRunRequest request,
        CreateRunHandler handler,
        CancellationToken ct)
    {
        if (!Enum.TryParse<RunStatus>(request.Status, ignoreCase: true, out RunStatus status))
        {
            status = RunStatus.Completed;
        }

        var command = new CreateRunCommand(
            id, request.Name, request.Model, request.InstanceId, request.Parameters,
            request.Input, request.Output, request.SystemPrompt, request.Metrics,
            request.PromptTokens, request.CompletionTokens, request.TotalTokens,
            request.Cost, request.LatencyMs, request.TtftMs, request.TokensPerSecond,
            request.Perplexity, request.LogprobsData, status, request.Error, request.Tags,
            request.FinishReason);

        Result<RunDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Created($"/api/v1/experiments/{id}/runs/{dto.Id}", dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListRuns(
        Guid id,
        [FromQuery] string? model,
        [FromQuery] string? status,
        [FromQuery] string? sortBy,
        [FromQuery] string? order,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        ListRunsHandler handler,
        CancellationToken ct)
    {
        RunStatus? statusFilter = null;
        if (status is not null && Enum.TryParse<RunStatus>(status, ignoreCase: true, out RunStatus parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var query = new ListRunsQuery(
            id, model, statusFilter, null,
            sortBy ?? "createdAt", order ?? "desc",
            page ?? 1, pageSize ?? 50);

        Result<PagedResult<RunDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetRun(
        Guid id,
        Guid runId,
        GetRunHandler handler,
        CancellationToken ct)
    {
        var query = new GetRunQuery(id, runId);
        Result<RunDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> DeleteRun(
        Guid id,
        Guid runId,
        DeleteRunHandler handler,
        CancellationToken ct)
    {
        var command = new DeleteRunCommand(id, runId);
        Result result = await handler.HandleAsync(command, ct);

        return result.ToHttpResult();
    }

    private static async Task<IResult> CompareRuns(
        Guid id,
        [FromBody] CompareRunsRequest request,
        CompareRunsHandler handler,
        CancellationToken ct)
    {
        var query = new CompareRunsQuery(id, request.RunIds);
        Result<RunComparisonDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ExportRuns(
        Guid id,
        [FromQuery] string format,
        ExportRunsHandler handler,
        CancellationToken ct)
    {
        var query = new ExportRunsQuery(id, format ?? "json");
        Result<ExportResult> result = await handler.HandleAsync(query, ct);

        return result.Match(
            export => TypedResults.File(export.Data, export.ContentType, export.FileName),
            error => error.ToHttpResult());
    }
}
