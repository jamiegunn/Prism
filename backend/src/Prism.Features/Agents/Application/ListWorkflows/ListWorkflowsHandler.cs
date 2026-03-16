using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Agents.Application.Dtos;
using Prism.Features.Agents.Domain;

namespace Prism.Features.Agents.Application.ListWorkflows;

/// <summary>
/// Query to list agent workflows with optional search.
/// </summary>
public sealed record ListWorkflowsQuery(string? Search);

/// <summary>
/// Handles listing agent workflows.
/// </summary>
public sealed class ListWorkflowsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListWorkflowsHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public ListWorkflowsHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lists all agent workflows, optionally filtered by search term.
    /// </summary>
    /// <param name="query">The list query parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A list of workflow DTOs.</returns>
    public async Task<Result<List<AgentWorkflowDto>>> HandleAsync(ListWorkflowsQuery query, CancellationToken ct)
    {
        IQueryable<AgentWorkflow> queryable = _db.Set<AgentWorkflow>()
            .AsNoTracking()
            .Include(w => w.Runs);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.ToLower();
            queryable = queryable.Where(w =>
                w.Name.ToLower().Contains(search) ||
                (w.Description != null && w.Description.ToLower().Contains(search)));
        }

        List<AgentWorkflowDto> workflows = await queryable
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => AgentWorkflowDto.FromEntity(w))
            .ToListAsync(ct);

        return workflows;
    }
}
