namespace Prism.Common.Inference.Runtime;

/// <summary>
/// Options that control runtime behavior for an inference execution.
/// Features pass these alongside the <see cref="Models.ChatRequest"/> to customize
/// recording, tagging, and analysis behavior.
/// </summary>
public sealed record InferenceRunOptions
{
    /// <summary>
    /// Gets the source module name for recording attribution (e.g., "Playground", "TokenExplorer").
    /// </summary>
    public string? SourceModule { get; init; }

    /// <summary>
    /// Gets tags to attach to the recorded run for filtering and search.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Gets the project identifier to associate with this run.
    /// </summary>
    public Guid? ProjectId { get; init; }

    /// <summary>
    /// Gets the experiment identifier to associate with this run.
    /// </summary>
    public Guid? ExperimentId { get; init; }

    /// <summary>
    /// Gets the prompt version identifier that produced the request.
    /// </summary>
    public Guid? PromptVersionId { get; init; }

    /// <summary>
    /// Gets the surprise threshold for token analysis.
    /// Tokens with probability below this value are flagged as surprise tokens.
    /// Defaults to 0.1 (10%).
    /// </summary>
    public double SurpriseThreshold { get; init; } = 0.1;

    /// <summary>
    /// Gets whether to skip recording this run in history.
    /// Useful for internal/system calls that should not appear in user-facing history.
    /// Defaults to false (all runs are recorded).
    /// </summary>
    public bool SkipRecording { get; init; }

    /// <summary>
    /// Gets the default run options with standard settings.
    /// </summary>
    public static InferenceRunOptions Default => new();
}
