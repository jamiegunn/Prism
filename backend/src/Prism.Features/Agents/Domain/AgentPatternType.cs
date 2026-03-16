namespace Prism.Features.Agents.Domain;

/// <summary>
/// Defines the execution pattern used by an agent workflow.
/// </summary>
public enum AgentPatternType
{
    /// <summary>Reasoning-Action-Observation loop pattern.</summary>
    ReAct,

    /// <summary>Sequential tool execution pattern.</summary>
    Sequential
}
