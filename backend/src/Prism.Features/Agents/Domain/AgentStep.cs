namespace Prism.Features.Agents.Domain;

/// <summary>
/// Represents a single step in an agent's execution trace.
/// Serialized as JSON within <see cref="AgentRun.StepsJson"/>.
/// </summary>
public sealed class AgentStep
{
    /// <summary>
    /// Gets or sets the zero-based step index.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the agent's reasoning/thought for this step.
    /// </summary>
    public string? Thought { get; set; }

    /// <summary>
    /// Gets or sets the tool name selected by the agent, or null for final answer.
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Gets or sets the input provided to the selected tool.
    /// </summary>
    public string? ActionInput { get; set; }

    /// <summary>
    /// Gets or sets the result returned by the tool execution.
    /// </summary>
    public string? Observation { get; set; }

    /// <summary>
    /// Gets or sets whether this step produced the final answer.
    /// </summary>
    public bool IsFinalAnswer { get; set; }

    /// <summary>
    /// Gets or sets the final answer text if <see cref="IsFinalAnswer"/> is true.
    /// </summary>
    public string? FinalAnswer { get; set; }

    /// <summary>
    /// Gets or sets the number of tokens consumed in this step's inference call.
    /// </summary>
    public int TokensUsed { get; set; }

    /// <summary>
    /// Gets or sets the latency in milliseconds for this step.
    /// </summary>
    public long LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets any error that occurred during this step.
    /// </summary>
    public string? Error { get; set; }
}
