using Microsoft.Extensions.DependencyInjection;
using Prism.Common.Database.Seeders;
using Prism.Features.Workspaces.Application.CreateWorkspace;
using Prism.Features.Workspaces.Application.GetWorkspace;
using Prism.Features.Workspaces.Application.ListWorkspaces;
using Prism.Features.Workspaces.Infrastructure;

namespace Prism.Features.Workspaces;

/// <summary>
/// Dependency injection module for the Workspaces feature.
/// </summary>
public static class WorkspacesModule
{
    /// <summary>
    /// Registers all Workspaces feature services.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWorkspacesFeature(this IServiceCollection services)
    {
        // Handlers
        services.AddScoped<CreateWorkspaceHandler>();
        services.AddScoped<ListWorkspacesHandler>();
        services.AddScoped<GetWorkspaceHandler>();

        // Seeders
        services.AddScoped<IDataSeeder, WorkspaceSeeder>();

        return services;
    }
}
