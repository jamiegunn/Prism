namespace Prism.Common.Inference;

/// <summary>
/// HTTP timeouts applied to inference provider clients.
/// </summary>
/// <remarks>
/// A single flat timeout previously applied to every operation, set to 10 seconds. Health
/// probes want a short deadline; a generation on a laptop-class model, a parameter sweep, an
/// agent step or an Ollama model pull may legitimately take minutes. Separating them lets a
/// dead endpoint fail fast without cancelling work that is progressing normally.
/// </remarks>
public sealed class InferenceClientOptions
{
    /// <summary>
    /// The configuration section these options bind to.
    /// </summary>
    public const string SectionName = "Inference:Timeouts";

    /// <summary>
    /// Gets or sets the ceiling for a single inference request, covering chat completions,
    /// sweeps and agent steps. Defaults to 5 minutes.
    /// </summary>
    public TimeSpan Request { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the deadline for health and capability probes, where a fast negative
    /// answer is more useful than a slow one. Defaults to 10 seconds.
    /// </summary>
    public TimeSpan Probe { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the deadline for long-running model management operations such as
    /// pulling or loading a model. Defaults to 30 minutes.
    /// </summary>
    public TimeSpan ModelManagement { get; set; } = TimeSpan.FromMinutes(30);
}
