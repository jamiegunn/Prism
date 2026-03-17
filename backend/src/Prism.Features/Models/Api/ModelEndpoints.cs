using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Results;
using Prism.Features.Models.Api.Requests;
using Prism.Features.Models.Application.CheckHealth;
using Prism.Features.Models.Application.Dtos;
using Prism.Features.Models.Application.GetInstanceMetrics;
using Prism.Features.Models.Application.ListInstances;
using Prism.Features.Models.Application.RegisterInstance;
using Prism.Features.Models.Application.SwapModel;
using Prism.Features.Models.Application.GetTokenizerInfo;
using Prism.Features.Models.Application.UnregisterInstance;
using Prism.Features.Models.Application.GetCapabilities;
using Prism.Features.Models.Application.ProbeCapabilities;
using Prism.Common.Inference.Capabilities;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Api;

/// <summary>
/// Defines the HTTP endpoints for managing inference provider instances.
/// </summary>
public static class ModelEndpoints
{
    /// <summary>
    /// Maps the model management endpoints under <c>/api/v1/models/instances</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder (typically the WebApplication).</param>
    /// <returns>The route group builder for further configuration.</returns>
    public static RouteGroupBuilder MapModelEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/models/instances")
            .WithTags("Models");

        group.MapPost("/", RegisterInstance)
            .WithName("RegisterInstance")
            .WithSummary("Registers a new inference provider instance")
            .Produces<InferenceInstanceDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/", ListInstances)
            .WithName("ListInstances")
            .WithSummary("Lists all registered inference instances with optional filters")
            .Produces<List<InferenceInstanceDto>>();

        group.MapGet("/{id:guid}", GetInstance)
            .WithName("GetInstance")
            .WithSummary("Gets a specific inference instance by ID")
            .Produces<InferenceInstanceDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", UnregisterInstance)
            .WithName("UnregisterInstance")
            .WithSummary("Unregisters an inference provider instance")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/metrics", GetInstanceMetrics)
            .WithName("GetInstanceMetrics")
            .WithSummary("Gets live performance metrics for an inference instance")
            .Produces<InstanceMetricsDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/swap-model", SwapModel)
            .WithName("SwapModel")
            .WithSummary("Hot-swaps the model loaded on an inference instance")
            .Produces<InferenceInstanceDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/{id:guid}/health-check", TriggerHealthCheck)
            .WithName("TriggerHealthCheck")
            .WithSummary("Triggers a manual health check on an inference instance")
            .Produces<InferenceInstanceDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/tokenizer", GetTokenizerInfo)
            .WithName("GetTokenizerInfo")
            .WithSummary("Gets tokenizer information for an inference instance including vocab size and special tokens")
            .Produces<TokenizerInfoDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/capabilities", GetCapabilities)
            .WithName("GetInstanceCapabilities")
            .WithSummary("Gets cached capability snapshot for an inference instance")
            .Produces<ProviderCapabilitySnapshot>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/probe", ProbeCapabilities)
            .WithName("ProbeInstanceCapabilities")
            .WithSummary("Probes an inference instance for its actual capabilities and updates the cached data")
            .Produces<ProviderCapabilitySnapshot>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/capabilities", ListAllCapabilities)
            .WithName("ListAllCapabilities")
            .WithSummary("Gets capability snapshots for all registered provider instances")
            .Produces<List<ProviderCapabilitySnapshot>>();

        return group;
    }

    private static async Task<IResult> RegisterInstance(
        [FromBody] RegisterInstanceRequest request,
        RegisterInstanceHandler handler,
        CancellationToken ct)
    {
        if (!Enum.TryParse<InferenceProviderType>(request.ProviderType, ignoreCase: true, out InferenceProviderType providerType))
        {
            return TypedResults.Problem(
                detail: $"Invalid provider type '{request.ProviderType}'. Valid values: {string.Join(", ", Enum.GetNames<InferenceProviderType>())}",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var command = new RegisterInstanceCommand(
            request.Name,
            request.Endpoint,
            providerType,
            request.IsDefault ?? false,
            request.Tags);

        Result<InferenceInstanceDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Created($"/api/v1/models/instances/{dto.Id}", dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListInstances(
        [FromQuery] string? status,
        [FromQuery] string? providerType,
        ListInstancesHandler handler,
        CancellationToken ct)
    {
        InstanceStatus? statusFilter = null;
        if (status is not null && Enum.TryParse<InstanceStatus>(status, ignoreCase: true, out InstanceStatus parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        InferenceProviderType? providerTypeFilter = null;
        if (providerType is not null && Enum.TryParse<InferenceProviderType>(providerType, ignoreCase: true, out InferenceProviderType parsedType))
        {
            providerTypeFilter = parsedType;
        }

        var query = new ListInstancesQuery(statusFilter, providerTypeFilter);
        Result<List<InferenceInstanceDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dtos => TypedResults.Ok(dtos),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetInstance(
        Guid id,
        AppDbContext db,
        CancellationToken ct)
    {
        InferenceInstance? instance = await db.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (instance is null)
        {
            return TypedResults.Problem(
                detail: $"Inference instance with ID '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(InferenceInstanceDto.FromEntity(instance));
    }

    private static async Task<IResult> UnregisterInstance(
        Guid id,
        UnregisterInstanceHandler handler,
        CancellationToken ct)
    {
        var command = new UnregisterInstanceCommand(id);
        Result result = await handler.HandleAsync(command, ct);

        return result.ToHttpResult();
    }

    private static async Task<IResult> GetInstanceMetrics(
        Guid id,
        GetInstanceMetricsHandler handler,
        CancellationToken ct)
    {
        var query = new GetInstanceMetricsQuery(id);
        Result<InstanceMetricsDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> SwapModel(
        Guid id,
        [FromBody] SwapModelRequest request,
        SwapModelHandler handler,
        CancellationToken ct)
    {
        var command = new SwapModelCommand(id, request.ModelId);
        Result<InferenceInstanceDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> TriggerHealthCheck(
        Guid id,
        CheckHealthHandler handler,
        CancellationToken ct)
    {
        var command = new CheckHealthCommand(id);
        Result<InferenceInstanceDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetTokenizerInfo(
        Guid id,
        GetTokenizerInfoHandler handler,
        CancellationToken ct)
    {
        var query = new GetTokenizerInfoQuery(id);
        Result<TokenizerInfoDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetCapabilities(
        Guid id,
        GetCapabilitiesHandler handler,
        CancellationToken ct)
    {
        Result<ProviderCapabilitySnapshot> result = await handler.HandleAsync(id, ct);

        return result.Match(
            snapshot => TypedResults.Ok(snapshot),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ProbeCapabilities(
        Guid id,
        ProbeCapabilitiesHandler handler,
        CancellationToken ct)
    {
        Result<ProviderCapabilitySnapshot> result = await handler.HandleAsync(id, ct);

        return result.Match(
            snapshot => TypedResults.Ok(snapshot),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListAllCapabilities(
        GetCapabilitiesHandler handler,
        CancellationToken ct)
    {
        IReadOnlyList<ProviderCapabilitySnapshot> snapshots = await handler.HandleAllAsync(ct);
        return TypedResults.Ok(snapshots);
    }
}
