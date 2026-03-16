namespace Prism.Features.Agents.Domain.Tools;

/// <summary>
/// Registry that manages available agent tools. Tools are registered at startup
/// and resolved by name during agent execution.
/// </summary>
public sealed class AgentToolRegistry
{
    private readonly Dictionary<string, IAgentTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a tool in the registry. Overwrites any existing tool with the same name.
    /// </summary>
    /// <param name="tool">The tool to register.</param>
    public void Register(IAgentTool tool)
    {
        _tools[tool.Name] = tool;
    }

    /// <summary>
    /// Attempts to get a tool by name.
    /// </summary>
    /// <param name="name">The tool name to look up.</param>
    /// <returns>The tool if found, otherwise null.</returns>
    public IAgentTool? Get(string name)
    {
        _tools.TryGetValue(name, out IAgentTool? tool);
        return tool;
    }

    /// <summary>
    /// Gets all registered tools.
    /// </summary>
    /// <returns>A read-only collection of all registered tools.</returns>
    public IReadOnlyCollection<IAgentTool> GetAll() => _tools.Values;

    /// <summary>
    /// Gets tools matching the specified names.
    /// </summary>
    /// <param name="names">The tool names to resolve.</param>
    /// <returns>The matching tools (unrecognized names are skipped).</returns>
    public IReadOnlyList<IAgentTool> GetByNames(IEnumerable<string> names)
    {
        var result = new List<IAgentTool>();
        foreach (string name in names)
        {
            if (_tools.TryGetValue(name, out IAgentTool? tool))
            {
                result.Add(tool);
            }
        }
        return result;
    }
}
