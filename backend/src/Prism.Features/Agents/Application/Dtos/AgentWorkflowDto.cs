using Prism.Features.Agents.Domain;

namespace Prism.Features.Agents.Application.Dtos;

/// <summary>
/// Data transfer object for an agent workflow.
/// </summary>
public sealed record AgentWorkflowDto(
    Guid Id,
    Guid? ProjectId,
    string Name,
    string? Description,
    string SystemPrompt,
    string Model,
    Guid InstanceId,
    string Pattern,
    int MaxSteps,
    int TokenBudget,
    double Temperature,
    List<string> EnabledTools,
    int Version,
    int RunCount,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>
    /// Creates a DTO from an <see cref="AgentWorkflow"/> entity.
    /// </summary>
    /// <param name="entity">The workflow entity.</param>
    /// <returns>A new <see cref="AgentWorkflowDto"/>.</returns>
    public static AgentWorkflowDto FromEntity(AgentWorkflow entity) => new(
        entity.Id,
        entity.ProjectId,
        entity.Name,
        entity.Description,
        entity.SystemPrompt,
        entity.Model,
        entity.InstanceId,
        entity.Pattern.ToString(),
        entity.MaxSteps,
        entity.TokenBudget,
        entity.Temperature,
        entity.EnabledTools,
        entity.Version,
        entity.Runs.Count,
        entity.CreatedAt,
        entity.UpdatedAt);
}
