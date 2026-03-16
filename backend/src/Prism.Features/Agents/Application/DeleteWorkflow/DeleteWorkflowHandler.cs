using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Agents.Domain;

namespace Prism.Features.Agents.Application.DeleteWorkflow;

/// <summary>
/// Command to delete an agent workflow and all its runs.
/// </summary>
public sealed record DeleteWorkflowCommand(Guid Id);

/// <summary>
/// Handles deletion of agent workflows.
/// </summary>
public sealed class DeleteWorkflowHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeleteWorkflowHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWorkflowHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public DeleteWorkflowHandler(AppDbContext db, ILogger<DeleteWorkflowHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Deletes an agent workflow and its associated runs.
    /// </summary>
    /// <param name="command">The delete command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> HandleAsync(DeleteWorkflowCommand command, CancellationToken ct)
    {
        AgentWorkflow? workflow = await _db.Set<AgentWorkflow>()
            .FirstOrDefaultAsync(w => w.Id == command.Id, ct);

        if (workflow is null)
            return Error.NotFound($"Agent workflow {command.Id} not found.");

        _db.Set<AgentWorkflow>().Remove(workflow);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted agent workflow {WorkflowId}", command.Id);

        return Result.Success();
    }
}
