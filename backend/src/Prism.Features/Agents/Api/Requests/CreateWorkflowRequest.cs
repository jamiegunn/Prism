namespace Prism.Features.Agents.Api.Requests;

/// <summary>
/// Request to create a new agent workflow.
/// </summary>
public sealed record CreateWorkflowRequest(
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
