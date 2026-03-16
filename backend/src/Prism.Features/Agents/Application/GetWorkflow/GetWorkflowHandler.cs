using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Agents.Application.Dtos;
using Prism.Features.Agents.Domain;

namespace Prism.Features.Agents.Application.GetWorkflow;

/// <summary>
/// Query to get a single agent workflow by ID.
/// </summary>
public sealed record GetWorkflowQuery(Guid Id);

/// <summary>
/// Handles retrieving a single agent workflow.
/// </summary>
public sealed class GetWorkflowHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetWorkflowHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public GetWorkflowHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Gets an agent workflow by ID.
    /// </summary>
    /// <param name="query">The get workflow query.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The workflow DTO if found.</returns>
    public async Task<Result<AgentWorkflowDto>> HandleAsync(GetWorkflowQuery query, CancellationToken ct)
    {
        AgentWorkflow? workflow = await _db.Set<AgentWorkflow>()
            .AsNoTracking()
            .Include(w => w.Runs)
            .FirstOrDefaultAsync(w => w.Id == query.Id, ct);

        if (workflow is null)
            return Error.NotFound($"Agent workflow {query.Id} not found.");

        return AgentWorkflowDto.FromEntity(workflow);
    }
}
