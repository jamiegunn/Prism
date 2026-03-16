using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Agents.Application.Dtos;
using Prism.Features.Agents.Domain;

namespace Prism.Features.Agents.Application.CreateWorkflow;

/// <summary>
/// Command to create a new agent workflow.
/// </summary>
public sealed record CreateWorkflowCommand(
    string Name,
    string? Description,
    string SystemPrompt,
    string Model,
    Guid InstanceId,
    AgentPatternType Pattern,
    int MaxSteps,
    int TokenBudget,
    double Temperature,
    List<string> EnabledTools);

/// <summary>
/// Handles creation of new agent workflows.
/// </summary>
public sealed class CreateWorkflowHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreateWorkflowHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateWorkflowHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public CreateWorkflowHandler(AppDbContext db, ILogger<CreateWorkflowHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new agent workflow.
    /// </summary>
    /// <param name="command">The create workflow command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The created workflow DTO.</returns>
    public async Task<Result<AgentWorkflowDto>> HandleAsync(CreateWorkflowCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Error.Validation("Workflow name is required.");

        if (string.IsNullOrWhiteSpace(command.Model))
            return Error.Validation("Model is required.");

        var workflow = new AgentWorkflow
        {
            Name = command.Name,
            Description = command.Description,
            SystemPrompt = command.SystemPrompt,
            Model = command.Model,
            InstanceId = command.InstanceId,
            Pattern = command.Pattern,
            MaxSteps = command.MaxSteps > 0 ? command.MaxSteps : 10,
            TokenBudget = command.TokenBudget > 0 ? command.TokenBudget : 8000,
            Temperature = command.Temperature,
            EnabledTools = command.EnabledTools
        };

        _db.Set<AgentWorkflow>().Add(workflow);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created agent workflow {WorkflowName} with ID {WorkflowId}", workflow.Name, workflow.Id);

        return AgentWorkflowDto.FromEntity(workflow);
    }
}
