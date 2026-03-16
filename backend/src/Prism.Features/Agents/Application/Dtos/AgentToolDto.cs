using Prism.Features.Agents.Domain.Tools;

namespace Prism.Features.Agents.Application.Dtos;

/// <summary>
/// Data transfer object describing an available agent tool.
/// </summary>
public sealed record AgentToolDto(
    string Name,
    string Description,
    string ParameterSchema)
{
    /// <summary>
    /// Creates a DTO from an <see cref="IAgentTool"/> instance.
    /// </summary>
    /// <param name="tool">The agent tool.</param>
    /// <returns>A new <see cref="AgentToolDto"/>.</returns>
    public static AgentToolDto FromTool(IAgentTool tool) => new(
        tool.Name,
        tool.Description,
        tool.ParameterSchema);
}
