namespace Prism.Features.Agents.Api.Requests;

/// <summary>
/// Request to update an existing agent workflow.
/// </summary>
public sealed record UpdateWorkflowRequest(
    string Name,
    string? Description,
    string SystemPrompt,
    string Model,
    Guid InstanceId,
    string Pattern,
    int MaxSteps,
    int TokenBudget,
    double Temperature,
    List<string> EnabledTools);
