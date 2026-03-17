using Prism.Common.Results;

namespace Prism.Common.Inference.Runtime;

/// <summary>
/// Re-executes a previously recorded inference run with optional overrides.
/// Produces a new <see cref="InferenceRunResult"/> linked to the original run.
/// </summary>
public interface IReplayService
{
    /// <summary>
    /// Replays a recorded run with optional overrides to the model, parameters, or prompt.
    /// </summary>
    /// <param name="request">The replay request specifying the original run and any overrides.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the new run result from the replay execution.</returns>
    Task<Result<InferenceRunResult>> ReplayAsync(ReplayRequest request, CancellationToken ct);
}

/// <summary>
/// Specifies a replay execution: the original run to replay and any overrides.
/// </summary>
public sealed record ReplayRequest
{
    /// <summary>
    /// Gets the ID of the original inference record to replay.
    /// </summary>
    public required Guid OriginalRunId { get; init; }

    /// <summary>
    /// Gets an optional override for the provider instance.
    /// If null, uses the same provider as the original run.
    /// </summary>
    public Guid? OverrideInstanceId { get; init; }

    /// <summary>
    /// Gets an optional override for the model identifier.
    /// If null, uses the same model as the original run.
    /// </summary>
    public string? OverrideModel { get; init; }

    /// <summary>
    /// Gets an optional temperature override.
    /// </summary>
    public double? OverrideTemperature { get; init; }

    /// <summary>
    /// Gets an optional max tokens override.
    /// </summary>
    public int? OverrideMaxTokens { get; init; }

    /// <summary>
    /// Gets an optional top-P override.
    /// </summary>
    public double? OverrideTopP { get; init; }

    /// <summary>
    /// Gets additional tags to attach to the replay run.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
