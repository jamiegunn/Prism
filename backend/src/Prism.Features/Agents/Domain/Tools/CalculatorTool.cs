using System.Data;

namespace Prism.Features.Agents.Domain.Tools;

/// <summary>
/// A tool that evaluates mathematical expressions.
/// Supports basic arithmetic operations: +, -, *, /, %, and parentheses.
/// </summary>
public sealed class CalculatorTool : IAgentTool
{
    /// <inheritdoc />
    public string Name => "calculator";

    /// <inheritdoc />
    public string Description => "Evaluates a mathematical expression and returns the numeric result. Supports +, -, *, /, %, and parentheses.";

    /// <inheritdoc />
    public string ParameterSchema => """{"type": "string", "description": "A mathematical expression to evaluate, e.g. '(2 + 3) * 4'"}""";

    /// <inheritdoc />
    public Task<ToolResult> ExecuteAsync(string input, CancellationToken ct)
    {
        try
        {
            string sanitized = input.Trim();

            // Basic safety check — only allow math characters
            if (!System.Text.RegularExpressions.Regex.IsMatch(sanitized, @"^[\d\s\+\-\*\/\%\.\(\)]+$"))
            {
                return Task.FromResult(ToolResult.Fail("Expression contains invalid characters. Only numbers and +, -, *, /, %, (, ) are allowed."));
            }

            var table = new DataTable();
            object? result = table.Compute(sanitized, null);
            string output = Convert.ToDouble(result).ToString("G");

            return Task.FromResult(ToolResult.Ok(output));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"Failed to evaluate expression: {ex.Message}"));
        }
    }
}
