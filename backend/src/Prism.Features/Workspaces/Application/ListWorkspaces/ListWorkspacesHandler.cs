using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Workspaces.Application.Dtos;
using Prism.Features.Workspaces.Domain;

namespace Prism.Features.Workspaces.Application.ListWorkspaces;

/// <summary>
/// Handles listing all workspaces.
/// </summary>
public sealed class ListWorkspacesHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListWorkspacesHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public ListWorkspacesHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lists all workspaces ordered by default first, then by name.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the list of workspace DTOs.</returns>
    public async Task<Result<List<WorkspaceDto>>> HandleAsync(CancellationToken ct)
    {
        List<Workspace> workspaces = await _db.Set<Workspace>()
            .AsNoTracking()
            .OrderByDescending(w => w.IsDefault)
            .ThenBy(w => w.Name)
            .ToListAsync(ct);

        return workspaces.Select(WorkspaceDto.FromEntity).ToList();
    }
}
