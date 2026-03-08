using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Api.Requests;

/// <summary>
/// HTTP request body for creating a new run.
/// </summary>
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
/// <param name="TokensPerSecond">The generation rate.</param>
/// <param name="Perplexity">The perplexity score.</param>
/// <param name="LogprobsData">The raw logprobs data as JSON.</param>
/// <param name="Status">The execution status (default: Completed).</param>
/// <param name="Error">The error message if failed.</param>
/// <param name="Tags">The tags for this run.</param>
/// <param name="FinishReason">The reason generation finished.</param>
public sealed record CreateRunRequest(
    string Model,
    string Input,
    string? Name = null,
    Guid? InstanceId = null,
    RunParameters? Parameters = null,
    string? Output = null,
    string? SystemPrompt = null,
    Dictionary<string, double>? Metrics = null,
    int PromptTokens = 0,
    int CompletionTokens = 0,
    int TotalTokens = 0,
    decimal? Cost = null,
    long LatencyMs = 0,
    int? TtftMs = null,
    double? TokensPerSecond = null,
    double? Perplexity = null,
    string? LogprobsData = null,
    string Status = "Completed",
    string? Error = null,
    List<string>? Tags = null,
    string? FinishReason = null);
