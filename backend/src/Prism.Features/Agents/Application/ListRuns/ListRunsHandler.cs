using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Agents.Application.Dtos;
using Prism.Features.Agents.Domain;

namespace Prism.Features.Agents.Application.ListRuns;

/// <summary>
/// Query to list runs for a specific agent workflow.
/// </summary>
public sealed record ListRunsQuery(Guid WorkflowId);

/// <summary>
/// Handles listing runs for an agent workflow.
/// </summary>
public sealed class ListRunsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListRunsHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public ListRunsHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lists all runs for the specified workflow.
    /// </summary>
    /// <param name="query">The list runs query.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A list of run DTOs.</returns>
    public async Task<Result<List<AgentRunDto>>> HandleAsync(ListRunsQuery query, CancellationToken ct)
    {
        List<AgentRunDto> runs = await _db.Set<AgentRun>()
            .AsNoTracking()
            .Where(r => r.WorkflowId == query.WorkflowId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct)
            .ContinueWith(t => t.Result.Select(AgentRunDto.FromEntity).ToList(), ct);

        return runs;
    }
}
