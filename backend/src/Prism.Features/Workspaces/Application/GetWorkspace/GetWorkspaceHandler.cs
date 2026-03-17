using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Workspaces.Application.Dtos;
using Prism.Features.Workspaces.Domain;

namespace Prism.Features.Workspaces.Application.GetWorkspace;

/// <summary>
/// Handles getting a single workspace by ID.
/// </summary>
public sealed class GetWorkspaceHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetWorkspaceHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public GetWorkspaceHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Gets a workspace by its unique identifier.
    /// </summary>
    /// <param name="id">The workspace ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the workspace DTO or a NotFound error.</returns>
    public async Task<Result<WorkspaceDto>> HandleAsync(Guid id, CancellationToken ct)
    {
        Workspace? workspace = await _db.Set<Workspace>()
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (workspace is null)
        {
            return Result<WorkspaceDto>.Failure(Error.NotFound($"Workspace {id} not found."));
        }

        return WorkspaceDto.FromEntity(workspace);
    }
}
