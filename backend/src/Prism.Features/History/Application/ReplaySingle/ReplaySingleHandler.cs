using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Features.History.Application.Dtos;
using Prism.Features.History.Application.GetRecord;
using Prism.Features.History.Domain;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;

namespace Prism.Features.History.Application.ReplaySingle;

/// <summary>
/// Handles replaying a recorded inference request against a specified provider instance.
/// Deserializes the original request, sends it to the target instance, and returns a
/// side-by-side comparison of original and replay responses.
/// </summary>
public sealed class ReplaySingleHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<ReplaySingleHandler> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaySingleHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="providerFactory">The factory for creating inference provider instances.</param>
    /// <param name="logger">The logger instance.</param>
    public ReplaySingleHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ILogger<ReplaySingleHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Replays the original inference request against the specified provider instance and returns
    /// a comparison of original and replay responses.
    /// </summary>
    /// <param name="command">The command containing the record ID and target instance ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the replay comparison, or an error if the record or instance is not found.</returns>
    public async Task<Result<ReplayResultDto>> HandleAsync(ReplaySingleCommand command, CancellationToken ct)
    {
        InferenceRecord? record = await _db.Set<InferenceRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == command.RecordId, ct);

        if (record is null)
        {
            _logger.LogWarning("Inference record {RecordId} was not found for replay", command.RecordId);
            return Error.NotFound($"Inference record '{command.RecordId}' was not found.");
        }

        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == command.InstanceId, ct);

        if (instance is null)
        {
            _logger.LogWarning("Inference instance {InstanceId} was not found for replay", command.InstanceId);
            return Error.NotFound($"Inference instance '{command.InstanceId}' was not found.");
        }

        ChatRequest? originalRequest = DeserializeRequest(record.RequestJson);
        if (originalRequest is null)
        {
            _logger.LogError("Failed to deserialize request JSON for record {RecordId}", command.RecordId);
            return Error.Internal($"Failed to deserialize the original request for record '{command.RecordId}'.");
        }

        // Apply overrides: command overrides > instance model > original request
        string replayModel = command.OverrideModel ?? instance.ModelId ?? originalRequest.Model;
        ChatRequest replayRequest = originalRequest with
        {
            Model = replayModel,
            Temperature = command.OverrideTemperature ?? originalRequest.Temperature,
            MaxTokens = command.OverrideMaxTokens ?? originalRequest.MaxTokens,
            TopP = command.OverrideTopP ?? originalRequest.TopP,
            Stream = false,
            SourceModule = "history-replay"
        };

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        Stopwatch stopwatch = Stopwatch.StartNew();
        Result<ChatResponse> replayResult = await provider.ChatAsync(replayRequest, ct);
        stopwatch.Stop();

        if (replayResult.IsFailure)
        {
            _logger.LogWarning(
                "Replay of record {RecordId} against instance {InstanceId} failed: {Error}",
                command.RecordId, command.InstanceId, replayResult.Error.Message);
            return Error.Unavailable($"Replay failed: {replayResult.Error.Message}");
        }

        ChatResponse replayResponse = replayResult.Value;
        InferenceRecordDetailDto originalDto = GetRecordHandler.MapToDetailDto(record);

        string? originalContent = ExtractOriginalContent(record.ResponseJson);
        string diffSummary = ComputeDiffSummary(originalContent, replayResponse.Content);

        var result = new ReplayResultDto(
            OriginalRecordId: command.RecordId,
            Original: originalDto,
            ReplayResponseContent: replayResponse.Content,
            ReplayPromptTokens: replayResponse.Usage?.PromptTokens ?? 0,
            ReplayCompletionTokens: replayResponse.Usage?.CompletionTokens ?? 0,
            ReplayLatencyMs: stopwatch.ElapsedMilliseconds,
            ReplayModel: replayModel,
            DiffSummary: diffSummary);

        _logger.LogInformation(
            "Replayed record {RecordId} against instance {InstanceId} in {LatencyMs}ms",
            command.RecordId, command.InstanceId, stopwatch.ElapsedMilliseconds);

        return result;
    }

    /// <summary>
    /// Deserializes a serialized ChatRequest JSON string back to a <see cref="ChatRequest"/> object.
    /// </summary>
    /// <param name="requestJson">The serialized JSON string.</param>
    /// <returns>The deserialized <see cref="ChatRequest"/>, or null if deserialization fails.</returns>
    private static ChatRequest? DeserializeRequest(string requestJson)
    {
        try
        {
            return JsonSerializer.Deserialize<ChatRequest>(requestJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts the response content from a serialized ChatResponse JSON string.
    /// </summary>
    /// <param name="responseJson">The serialized ChatResponse JSON, or null.</param>
    /// <returns>The response content string, or null if extraction fails.</returns>
    private static string? ExtractOriginalContent(string? responseJson)
    {
        if (responseJson is null)
        {
            return null;
        }

        try
        {
            ChatResponse? response = JsonSerializer.Deserialize<ChatResponse>(responseJson, JsonOptions);
            return response?.Content;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Computes a simple textual summary of the differences between the original and replay responses.
    /// </summary>
    /// <param name="originalContent">The original response content, or null.</param>
    /// <param name="replayContent">The replay response content.</param>
    /// <returns>A human-readable diff summary string.</returns>
    private static string ComputeDiffSummary(string? originalContent, string replayContent)
    {
        if (originalContent is null)
        {
            return $"Original had no response; replay produced {replayContent.Length} characters";
        }

        if (originalContent == replayContent)
        {
            return "Responses are identical";
        }

        int lengthDiff = Math.Abs(originalContent.Length - replayContent.Length);
        int minLength = Math.Min(originalContent.Length, replayContent.Length);

        int firstDiffPosition = 0;
        for (int i = 0; i < minLength; i++)
        {
            if (originalContent[i] != replayContent[i])
            {
                firstDiffPosition = i;
                break;
            }

            firstDiffPosition = i + 1;
        }

        return $"Character length difference: {lengthDiff} ({originalContent.Length} vs {replayContent.Length}); first difference at position {firstDiffPosition}";
    }
}
