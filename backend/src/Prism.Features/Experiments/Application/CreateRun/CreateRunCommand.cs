using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.CreateRun;

/// <summary>
/// Command to create a new run in an experiment.
/// </summary>
/// <param name="ExperimentId">The parent experiment ID.</param>
/// <param name="Name">The optional display name.</param>
/// <param name="Model">The model identifier used.</param>
/// <param name="InstanceId">The optional inference provider instance ID.</param>
/// <param name="Parameters">The inference parameters used.</param>
/// <param name="Input">The input text sent to the model.</param>
/// <param name="Output">The generated output text.</param>
/// <param name="SystemPrompt">The optional system prompt used.</param>
/// <param name="Metrics">Custom metrics for this run.</param>
/// <param name="PromptTokens">The number of prompt tokens consumed.</param>
/// <param name="CompletionTokens">The number of completion tokens generated.</param>
/// <param name="TotalTokens">The total tokens used.</param>
/// <param name="Cost">The estimated cost in USD.</param>
/// <param name="LatencyMs">The total latency in milliseconds.</param>
/// <param name="TtftMs">The time to first token in milliseconds.</param>
/// <param name="TokensPerSecond">The generation rate in tokens per second.</param>
/// <param name="Perplexity">The perplexity score.</param>
/// <param name="LogprobsData">The raw logprobs data as JSON.</param>
/// <param name="Status">The execution status.</param>
/// <param name="Error">The error message if failed.</param>
/// <param name="Tags">The tags for this run.</param>
/// <param name="FinishReason">The reason generation finished.</param>
public sealed record CreateRunCommand(
    Guid ExperimentId,
    string? Name,
    string Model,
    Guid? InstanceId,
    RunParameters? Parameters,
    string Input,
    string? Output,
    string? SystemPrompt,
    Dictionary<string, double>? Metrics,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    decimal? Cost,
    long LatencyMs,
    int? TtftMs,
    double? TokensPerSecond,
    double? Perplexity,
    string? LogprobsData,
    RunStatus Status,
    string? Error,
    List<string>? Tags,
    string? FinishReason);
