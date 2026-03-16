using Prism.Common.Results;

namespace Prism.Features.Agents.Domain.Tools;

/// <summary>
/// Defines a tool that an agent can invoke during execution.
/// Each tool has a name, description, parameter schema, and execution logic.
/// </summary>
public interface IAgentTool
{
    /// <summary>
    /// Gets the unique name of this tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a human-readable description of what this tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the JSON schema describing the tool's expected input parameters.
    /// </summary>
    string ParameterSchema { get; }

    /// <summary>
    /// Executes the tool with the given input and returns the result.
    /// </summary>
    /// <param name="input">The input string to the tool.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The tool execution result.</returns>
    Task<ToolResult> ExecuteAsync(string input, CancellationToken ct);
}

/// <summary>
/// Represents the result of a tool execution.
/// </summary>
/// <param name="Success">Whether the tool executed successfully.</param>
/// <param name="Output">The output text from the tool.</param>
/// <param name="Error">The error message if execution failed.</param>
public sealed record ToolResult(bool Success, string Output, string? Error = null)
{
    /// <summary>
    /// Creates a successful tool result with the given output.
    /// </summary>
    /// <param name="output">The tool output text.</param>
    /// <returns>A successful <see cref="ToolResult"/>.</returns>
    public static ToolResult Ok(string output) => new(true, output);

    /// <summary>
    /// Creates a failed tool result with the given error message.
    /// </summary>
    /// <param name="error">The error description.</param>
    /// <returns>A failed <see cref="ToolResult"/>.</returns>
    public static ToolResult Fail(string error) => new(false, "", error);
}
