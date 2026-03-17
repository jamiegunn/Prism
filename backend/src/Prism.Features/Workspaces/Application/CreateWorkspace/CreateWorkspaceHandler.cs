using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Workspaces.Application.Dtos;
using Prism.Features.Workspaces.Domain;

namespace Prism.Features.Workspaces.Application.CreateWorkspace;

/// <summary>
/// Handles creation of a new workspace.
/// </summary>
public sealed class CreateWorkspaceHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreateWorkspaceHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateWorkspaceHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger.</param>
    public CreateWorkspaceHandler(AppDbContext db, ILogger<CreateWorkspaceHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new workspace.
    /// </summary>
    /// <param name="command">The creation command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the created workspace DTO.</returns>
    public async Task<Result<WorkspaceDto>> HandleAsync(CreateWorkspaceCommand command, CancellationToken ct)
    {
        var workspace = new Workspace
        {
            Name = command.Name,
            Description = command.Description,
            IsDefault = false,
            IconColor = command.IconColor
        };

        _db.Set<Workspace>().Add(workspace);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created workspace {WorkspaceName} with ID {WorkspaceId}", workspace.Name, workspace.Id);

        return WorkspaceDto.FromEntity(workspace);
    }
}

/// <summary>
/// Command for creating a workspace.
/// </summary>
/// <param name="Name">The workspace name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="IconColor">Optional icon color.</param>
public sealed record CreateWorkspaceCommand(string Name, string? Description = null, string? IconColor = null);
