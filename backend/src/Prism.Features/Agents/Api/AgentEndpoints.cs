using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Prism.Common.Results;
using Prism.Features.Agents.Api.Requests;
using Prism.Features.Agents.Application.CreateWorkflow;
using Prism.Features.Agents.Application.DeleteWorkflow;
using Prism.Features.Agents.Application.Dtos;
using Prism.Features.Agents.Application.GetRun;
using Prism.Features.Agents.Application.GetWorkflow;
using Prism.Features.Agents.Application.ListRuns;
using Prism.Features.Agents.Application.ListTools;
using Prism.Features.Agents.Application.ListWorkflows;
using Prism.Features.Agents.Application.RunAgent;
using Prism.Features.Agents.Application.UpdateWorkflow;
using Prism.Features.Agents.Domain;

namespace Prism.Features.Agents.Api;

/// <summary>
/// Defines the HTTP endpoints for the Agents feature including workflow CRUD,
/// execution with SSE streaming, and tool listing.
/// </summary>
public static class AgentEndpoints
{
    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Maps the agent endpoints under <c>/api/v1/agents</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group builder for further configuration.</returns>
    public static RouteGroupBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/agents")
            .WithTags("Agents");

        // Workflow CRUD
        group.MapPost("/", CreateWorkflow)
            .WithName("CreateAgentWorkflow")
            .WithSummary("Creates a new agent workflow")
            .Produces<AgentWorkflowDto>(StatusCodes.Status200OK);

        group.MapGet("/", ListWorkflows)
            .WithName("ListAgentWorkflows")
            .WithSummary("Lists all agent workflows")
            .Produces<List<AgentWorkflowDto>>();

        group.MapGet("/{id:guid}", GetWorkflow)
            .WithName("GetAgentWorkflow")
            .WithSummary("Gets an agent workflow by ID")
            .Produces<AgentWorkflowDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateWorkflow)
            .WithName("UpdateAgentWorkflow")
            .WithSummary("Updates an agent workflow")
            .Produces<AgentWorkflowDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteWorkflow)
            .WithName("DeleteAgentWorkflow")
            .WithSummary("Deletes an agent workflow and all its runs")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Execution
        group.MapPost("/{id:guid}/run", RunAgent)
            .WithName("RunAgent")
            .WithSummary("Executes an agent workflow with SSE streaming")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream");

        group.MapGet("/{id:guid}/runs", ListRuns)
            .WithName("ListAgentRuns")
            .WithSummary("Lists runs for an agent workflow")
            .Produces<List<AgentRunDto>>();

        group.MapGet("/runs/{runId:guid}", GetRun)
            .WithName("GetAgentRun")
            .WithSummary("Gets a specific agent run with execution trace")
            .Produces<AgentRunDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Tools
        group.MapGet("/tools", ListTools)
            .WithName("ListAgentTools")
            .WithSummary("Lists all available agent tools")
            .Produces<List<AgentToolDto>>();

        return group;
    }

    private static async Task<IResult> CreateWorkflow(
        [FromBody] CreateWorkflowRequest request,
        CreateWorkflowHandler handler,
        CancellationToken ct)
    {
        AgentPatternType pattern = Enum.TryParse<AgentPatternType>(request.Pattern, true, out AgentPatternType p)
            ? p
            : AgentPatternType.ReAct;

        var command = new CreateWorkflowCommand(
            request.Name,
            request.Description,
            request.SystemPrompt,
            request.Model,
            request.InstanceId,
            pattern,
            request.MaxSteps,
            request.TokenBudget,
            request.Temperature,
            request.EnabledTools);

        Result<AgentWorkflowDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListWorkflows(
        [FromQuery] string? search,
        ListWorkflowsHandler handler,
        CancellationToken ct)
    {
        var query = new ListWorkflowsQuery(search);
        Result<List<AgentWorkflowDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            list => TypedResults.Ok(list),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetWorkflow(
        Guid id,
        GetWorkflowHandler handler,
        CancellationToken ct)
    {
        var query = new GetWorkflowQuery(id);
        Result<AgentWorkflowDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> UpdateWorkflow(
        Guid id,
        [FromBody] UpdateWorkflowRequest request,
        UpdateWorkflowHandler handler,
        CancellationToken ct)
    {
        AgentPatternType pattern = Enum.TryParse<AgentPatternType>(request.Pattern, true, out AgentPatternType p)
            ? p
            : AgentPatternType.ReAct;

        var command = new UpdateWorkflowCommand(
            id,
            request.Name,
            request.Description,
            request.SystemPrompt,
            request.Model,
            request.InstanceId,
            pattern,
            request.MaxSteps,
            request.TokenBudget,
            request.Temperature,
            request.EnabledTools);

        Result<AgentWorkflowDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> DeleteWorkflow(
        Guid id,
        DeleteWorkflowHandler handler,
        CancellationToken ct)
    {
        var command = new DeleteWorkflowCommand(id);
        Result result = await handler.HandleAsync(command, ct);

        return result.ToHttpResult();
    }

    private static async Task RunAgent(
        Guid id,
        [FromBody] RunAgentRequest request,
        RunAgentHandler handler,
        HttpContext httpContext,
        CancellationToken ct)
    {
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        var command = new RunAgentCommand(id, request.Input);

        await foreach (AgentRunEvent runEvent in handler.HandleAsync(command, ct))
        {
            string eventType = runEvent switch
            {
                AgentRunStarted => "started",
                AgentStepCompleted => "step",
                AgentRunFinished => "finished",
                AgentRunError => "error",
                _ => "unknown"
            };

            string json = JsonSerializer.Serialize(runEvent, runEvent.GetType(), SseJsonOptions);
            await httpContext.Response.WriteAsync($"event: {eventType}\ndata: {json}\n\n", ct);
            await httpContext.Response.Body.FlushAsync(ct);
        }
    }

    private static async Task<IResult> ListRuns(
        Guid id,
        ListRunsHandler handler,
        CancellationToken ct)
    {
        var query = new ListRunsQuery(id);
        Result<List<AgentRunDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            list => TypedResults.Ok(list),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetRun(
        Guid runId,
        GetRunHandler handler,
        CancellationToken ct)
    {
        var query = new GetRunQuery(runId);
        Result<AgentRunDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListTools(
        ListToolsHandler handler,
        CancellationToken ct)
    {
        Result<List<AgentToolDto>> result = await handler.HandleAsync(ct);

        return result.Match(
            list => TypedResults.Ok(list),
            error => error.ToHttpResult());
    }
}
