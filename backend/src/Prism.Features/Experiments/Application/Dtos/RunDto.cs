using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.Dtos;

/// <summary>
/// Data transfer object for an experiment run.
/// </summary>
/// <param name="Id">The unique run identifier.</param>
/// <param name="ExperimentId">The parent experiment ID.</param>
/// <param name="Name">The optional display name.</param>
/// <param name="Model">The model identifier used.</param>
/// <param name="InstanceId">The optional inference provider instance ID.</param>
/// <param name="Parameters">The inference parameters used.</param>
/// <param name="Input">The input text sent to the model.</param>
/// <param name="Output">The generated output text.</param>
/// <param name="SystemPrompt">The optional system prompt used.</param>
/// <param name="Metrics">Custom metrics recorded for this run.</param>
/// <param name="PromptTokens">The number of prompt tokens consumed.</param>
/// <param name="CompletionTokens">The number of completion tokens generated.</param>
/// <param name="TotalTokens">The total tokens used.</param>
/// <param name="Cost">The estimated cost in USD.</param>
/// <param name="LatencyMs">The total latency in milliseconds.</param>
/// <param name="TtftMs">The time to first token in milliseconds.</param>
/// <param name="TokensPerSecond">The generation rate in tokens per second.</param>
/// <param name="Perplexity">The perplexity score.</param>
/// <param name="Status">The execution status.</param>
/// <param name="Error">The error message if the run failed.</param>
/// <param name="Tags">The tags associated with this run.</param>
/// <param name="FinishReason">The reason generation finished.</param>
/// <param name="CreatedAt">The UTC timestamp when the run was created.</param>
public sealed record RunDto(
    Guid Id,
    Guid ExperimentId,
    string? Name,
    string Model,
    Guid? InstanceId,
    RunParameters Parameters,
    string Input,
    string? Output,
    string? SystemPrompt,
    Dictionary<string, double> Metrics,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    decimal? Cost,
    long LatencyMs,
    int? TtftMs,
    double? TokensPerSecond,
    double? Perplexity,
    RunStatus Status,
    string? Error,
    List<string> Tags,
    string? FinishReason,
    DateTime CreatedAt)
{
    /// <summary>
    /// Creates a <see cref="RunDto"/> from a <see cref="Run"/> entity.
    /// </summary>
    /// <param name="entity">The run entity to map.</param>
    /// <returns>A new <see cref="RunDto"/> instance.</returns>
    public static RunDto FromEntity(Run entity)
    {
        return new RunDto(
            entity.Id,
            entity.ExperimentId,
            entity.Name,
            entity.Model,
            entity.InstanceId,
            entity.Parameters,
            entity.Input,
            entity.Output,
            entity.SystemPrompt,
            entity.Metrics,
            entity.PromptTokens,
            entity.CompletionTokens,
            entity.TotalTokens,
            entity.Cost,
            entity.LatencyMs,
            entity.TtftMs,
            entity.TokensPerSecond,
            entity.Perplexity,
            entity.Status,
            entity.Error,
            entity.Tags,
            entity.FinishReason,
            entity.CreatedAt);
    }
}
