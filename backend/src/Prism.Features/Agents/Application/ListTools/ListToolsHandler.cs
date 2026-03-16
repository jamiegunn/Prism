using Prism.Common.Results;
using Prism.Features.Agents.Application.Dtos;
using Prism.Features.Agents.Domain.Tools;

namespace Prism.Features.Agents.Application.ListTools;

/// <summary>
/// Handles listing all available agent tools.
/// </summary>
public sealed class ListToolsHandler
{
    private readonly AgentToolRegistry _toolRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListToolsHandler"/> class.
    /// </summary>
    /// <param name="toolRegistry">The agent tool registry.</param>
    public ListToolsHandler(AgentToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry;
    }

    /// <summary>
    /// Lists all available agent tools.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A list of tool DTOs.</returns>
    public Task<Result<List<AgentToolDto>>> HandleAsync(CancellationToken ct)
    {
        List<AgentToolDto> tools = _toolRegistry.GetAll()
            .Select(AgentToolDto.FromTool)
            .OrderBy(t => t.Name)
            .ToList();

        return Task.FromResult<Result<List<AgentToolDto>>>(tools);
    }
}
