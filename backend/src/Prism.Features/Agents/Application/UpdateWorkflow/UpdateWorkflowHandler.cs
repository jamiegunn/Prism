using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Agents.Application.Dtos;
using Prism.Features.Agents.Domain;

namespace Prism.Features.Agents.Application.UpdateWorkflow;

/// <summary>
/// Command to update an existing agent workflow.
/// </summary>
public sealed record UpdateWorkflowCommand(
    Guid Id,
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
/// Handles updating an existing agent workflow.
/// </summary>
public sealed class UpdateWorkflowHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<UpdateWorkflowHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWorkflowHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public UpdateWorkflowHandler(AppDbContext db, ILogger<UpdateWorkflowHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Updates an existing agent workflow.
    /// </summary>
    /// <param name="command">The update workflow command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The updated workflow DTO.</returns>
    public async Task<Result<AgentWorkflowDto>> HandleAsync(UpdateWorkflowCommand command, CancellationToken ct)
    {
        AgentWorkflow? workflow = await _db.Set<AgentWorkflow>()
            .Include(w => w.Runs)
            .FirstOrDefaultAsync(w => w.Id == command.Id, ct);

        if (workflow is null)
            return Error.NotFound($"Agent workflow {command.Id} not found.");

        workflow.Name = command.Name;
        workflow.Description = command.Description;
        workflow.SystemPrompt = command.SystemPrompt;
        workflow.Model = command.Model;
        workflow.InstanceId = command.InstanceId;
        workflow.Pattern = command.Pattern;
        workflow.MaxSteps = command.MaxSteps > 0 ? command.MaxSteps : 10;
        workflow.TokenBudget = command.TokenBudget > 0 ? command.TokenBudget : 8000;
        workflow.Temperature = command.Temperature;
        workflow.EnabledTools = command.EnabledTools;
        workflow.Version++;
        workflow.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated agent workflow {WorkflowId} to version {Version}", workflow.Id, workflow.Version);

        return AgentWorkflowDto.FromEntity(workflow);
    }
}
