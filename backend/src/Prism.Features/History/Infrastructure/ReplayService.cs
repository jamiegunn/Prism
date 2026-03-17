using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Inference.Models;
using Prism.Common.Inference.Runtime;
using Prism.Common.Results;
using Prism.Features.History.Domain;
using Prism.Features.Models.Domain;

namespace Prism.Features.History.Infrastructure;

/// <summary>
/// Default implementation of <see cref="IReplayService"/>.
/// Loads the original run from history, applies overrides, and re-executes through the runtime.
/// </summary>
public sealed class ReplayService : IReplayService
{
    private readonly AppDbContext _context;
    private readonly IInferenceRuntime _runtime;
    private readonly ILogger<ReplayService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplayService"/> class.
    /// </summary>
    /// <param name="context">The database context for loading original run records.</param>
    /// <param name="runtime">The inference runtime for executing the replay.</param>
    /// <param name="logger">The logger for replay operations.</param>
    public ReplayService(AppDbContext context, IInferenceRuntime runtime, ILogger<ReplayService> logger)
    {
        _context = context;
        _runtime = runtime;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<InferenceRunResult>> ReplayAsync(ReplayRequest request, CancellationToken ct)
    {
        InferenceRecord? originalRecord = await _context.Set<InferenceRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.OriginalRunId, ct);

        if (originalRecord is null)
        {
            return Result<InferenceRunResult>.Failure(
                Error.NotFound($"Inference record {request.OriginalRunId} not found."));
        }

        ChatRequest? originalRequest = DeserializeRequest(originalRecord.RequestJson);
        if (originalRequest is null)
        {
            return Result<InferenceRunResult>.Failure(
                Error.Validation("Could not deserialize the original request from history."));
        }

        // Apply overrides
        ChatRequest replayRequest = originalRequest with
        {
            Model = request.OverrideModel ?? originalRequest.Model,
            Temperature = request.OverrideTemperature ?? originalRequest.Temperature,
            MaxTokens = request.OverrideMaxTokens ?? originalRequest.MaxTokens,
            TopP = request.OverrideTopP ?? originalRequest.TopP
        };

        // Resolve instance: use override if provided, otherwise try to find original
        Guid instanceId = request.OverrideInstanceId
            ?? await ResolveOriginalInstanceId(originalRecord, ct);

        List<string> tags = ["replay", $"replay-of:{request.OriginalRunId}"];
        tags.AddRange(request.Tags);

        InferenceRunOptions options = new()
        {
            SourceModule = originalRecord.SourceModule ?? "Replay",
            Tags = tags
        };

        _logger.LogInformation(
            "Replaying run {OriginalRunId} on instance {InstanceId} with model {Model}",
            request.OriginalRunId, instanceId, replayRequest.Model);

        return await _runtime.ExecuteAsync(instanceId, replayRequest, options, ct);
    }

    /// <summary>
    /// Attempts to resolve the provider instance ID from the original record's endpoint.
    /// </summary>
    private async Task<Guid> ResolveOriginalInstanceId(InferenceRecord record, CancellationToken ct)
    {
        InferenceInstance? instance = await _context.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Endpoint == record.ProviderEndpoint, ct);

        return instance?.Id ?? Guid.Empty;
    }

    /// <summary>
    /// Deserializes the stored request JSON back into a <see cref="ChatRequest"/>.
    /// </summary>
    private static ChatRequest? DeserializeRequest(string? requestJson)
    {
        if (string.IsNullOrEmpty(requestJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ChatRequest>(requestJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }
}
