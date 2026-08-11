using Prism.Features.History.Domain;

namespace Prism.Features.History.Application.ExportHistory;

/// <summary>
/// One exported history record: every scalar column of <c>history_records</c>, in the shape a
/// researcher's tooling reads. Null means "not measured" and must survive the trip as null in
/// every format — a missing perplexity that becomes <c>0.0</c> in a file poisons every
/// aggregate computed from that file.
/// </summary>
/// <param name="Id">The record's unique identifier.</param>
/// <param name="SourceModule">Which Prism module issued the call.</param>
/// <param name="ProviderName">The display name of the provider instance.</param>
/// <param name="ProviderType">The provider type (for example <c>Ollama</c> or <c>Vllm</c>).</param>
/// <param name="ProviderEndpoint">The provider's base URL.</param>
/// <param name="Model">The model the request named.</param>
/// <param name="RequestJson">The serialized request as sent.</param>
/// <param name="ResponseJson">The serialized response, or null when the call failed before one existed.</param>
/// <param name="PromptTokens">Prompt token count.</param>
/// <param name="CompletionTokens">Completion token count.</param>
/// <param name="TotalTokens">Total token count.</param>
/// <param name="LatencyMs">End-to-end latency in milliseconds.</param>
/// <param name="TtftMs">Time to first token in milliseconds, or null when not measured.</param>
/// <param name="Perplexity">Perplexity computed from logprobs, or null when no logprobs were recorded.</param>
/// <param name="MeanEntropy">Mean per-token entropy, or null when no logprobs were recorded.</param>
/// <param name="SurpriseTokenCount">Count of low-probability tokens, or null when no logprobs were recorded.</param>
/// <param name="TokensPerSecond">Decode rate, or null when not measured.</param>
/// <param name="EstimatedCost">Estimated cost in USD, or null when no price is configured.</param>
/// <param name="IsSuccess">Whether the call succeeded.</param>
/// <param name="ErrorMessage">The failure message, or null on success.</param>
/// <param name="Tags">User-assigned tags.</param>
/// <param name="StartedAt">When the call started (UTC).</param>
/// <param name="CompletedAt">When the call completed (UTC).</param>
public sealed record HistoryExportRow(
    Guid Id,
    string SourceModule,
    string ProviderName,
    string ProviderType,
    string ProviderEndpoint,
    string Model,
    string RequestJson,
    string? ResponseJson,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    long LatencyMs,
    int? TtftMs,
    double? Perplexity,
    double? MeanEntropy,
    int? SurpriseTokenCount,
    double? TokensPerSecond,
    decimal? EstimatedCost,
    bool IsSuccess,
    string? ErrorMessage,
    List<string> Tags,
    DateTime StartedAt,
    DateTime CompletedAt)
{
    /// <summary>
    /// Maps a persisted record to its export row.
    /// </summary>
    /// <param name="record">The entity to map.</param>
    /// <returns>The export row.</returns>
    public static HistoryExportRow FromEntity(InferenceRecord record) => new(
        record.Id,
        record.SourceModule,
        record.ProviderName,
        record.ProviderType.ToString(),
        record.ProviderEndpoint,
        record.Model,
        record.RequestJson,
        record.ResponseJson,
        record.PromptTokens,
        record.CompletionTokens,
        record.TotalTokens,
        record.LatencyMs,
        record.TtftMs,
        record.Perplexity,
        record.MeanEntropy,
        record.SurpriseTokenCount,
        record.TokensPerSecond,
        record.EstimatedCost,
        record.IsSuccess,
        record.ErrorMessage,
        record.Tags,
        record.StartedAt,
        record.CompletedAt);
}
