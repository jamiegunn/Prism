namespace Prism.Features.Agents.Domain.Tools;

/// <summary>
/// A simple tool that echoes back its input. Useful for testing agent workflows.
/// </summary>
public sealed class EchoTool : IAgentTool
{
    /// <inheritdoc />
    public string Name => "echo";

    /// <inheritdoc />
    public string Description => "Echoes back the input text. Useful for testing.";

    /// <inheritdoc />
    public string ParameterSchema => """{"type": "string", "description": "Text to echo back"}""";

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(string input, CancellationToken ct)
    {
        return Task.FromResult(ToolResult.Ok(input));
    }
}
