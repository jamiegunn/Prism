using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Agents.Application.Dtos;
using Prism.Features.Agents.Domain;

namespace Prism.Features.Agents.Application.GetRun;

/// <summary>
/// Query to get a specific agent run by ID.
/// </summary>
public sealed record GetRunQuery(Guid RunId);

/// <summary>
/// Handles retrieving a single agent run with its execution trace.
/// </summary>
public sealed class GetRunHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetRunHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public GetRunHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Gets an agent run by ID.
    /// </summary>
    /// <param name="query">The get run query.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The run DTO if found.</returns>
    public async Task<Result<AgentRunDto>> HandleAsync(GetRunQuery query, CancellationToken ct)
    {
        AgentRun? run = await _db.Set<AgentRun>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == query.RunId, ct);

        if (run is null)
            return Error.NotFound($"Agent run {query.RunId} not found.");

        return AgentRunDto.FromEntity(run);
    }
}
