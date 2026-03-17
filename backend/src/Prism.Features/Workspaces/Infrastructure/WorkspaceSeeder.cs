using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Database.Seeders;
using Prism.Features.Workspaces.Domain;

namespace Prism.Features.Workspaces.Infrastructure;

/// <summary>
/// Seeds a default workspace on first launch.
/// </summary>
public sealed class WorkspaceSeeder : IDataSeeder
{
    /// <summary>
    /// Well-known ID for the default workspace, used by other seeders.
    /// </summary>
    public static readonly Guid DefaultWorkspaceId = Guid.Parse("00000000-0000-0000-0000-000000000010");

    /// <summary>
    /// Gets the execution order. Workspaces seed first at order 5.
    /// </summary>
    public int Order => 5;

    /// <summary>
    /// Seeds the default workspace if none exists.
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <param name="ct">A token to cancel the seeding operation.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task SeedAsync(AppDbContext context, CancellationToken ct)
    {
        bool hasWorkspaces = await context.Set<Workspace>().AnyAsync(ct);
        if (hasWorkspaces) return;

        var workspace = new Workspace
        {
            Id = DefaultWorkspaceId,
            Name = "Default Workspace",
            Description = "Your default research workspace. All projects and data live here.",
            IsDefault = true,
            IconColor = "violet"
        };

        context.Set<Workspace>().Add(workspace);
        await context.SaveChangesAsync(ct);
    }
}
