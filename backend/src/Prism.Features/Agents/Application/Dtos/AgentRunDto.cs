using System.Text.Json;
using Prism.Features.Agents.Domain;

namespace Prism.Features.Agents.Application.Dtos;

/// <summary>
/// Data transfer object for an agent run with its execution trace.
/// </summary>
public sealed record AgentRunDto(
    Guid Id,
    Guid WorkflowId,
    string Status,
    string Input,
    string? Output,
    string? ErrorMessage,
    List<AgentStep> Steps,
    int StepCount,
    int TotalTokens,
    long TotalLatencyMs,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime CreatedAt)
{
    /// <summary>
    /// Creates a DTO from an <see cref="AgentRun"/> entity.
    /// </summary>
    /// <param name="entity">The run entity.</param>
    /// <returns>A new <see cref="AgentRunDto"/>.</returns>
    public static AgentRunDto FromEntity(AgentRun entity)
    {
        List<AgentStep> steps;
        try
        {
            steps = JsonSerializer.Deserialize<List<AgentStep>>(entity.StepsJson) ?? [];
        }
        catch
        {
            steps = [];
        }

        return new AgentRunDto(
            entity.Id,
            entity.WorkflowId,
            entity.Status.ToString(),
            entity.Input,
            entity.Output,
            entity.ErrorMessage,
            steps,
            entity.StepCount,
            entity.TotalTokens,
            entity.TotalLatencyMs,
            entity.StartedAt,
            entity.CompletedAt,
            entity.CreatedAt);
    }
}
