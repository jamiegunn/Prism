using Microsoft.Extensions.DependencyInjection;
using Prism.Features.Notebooks.Application.CreateNotebook;
using Prism.Features.Notebooks.Application.DeleteNotebook;
using Prism.Features.Notebooks.Application.GetNotebook;
using Prism.Features.Notebooks.Application.ListNotebooks;
using Prism.Features.Notebooks.Application.UpdateNotebook;

namespace Prism.Features.Notebooks;

/// <summary>
/// Registers all Notebook services in the dependency injection container.
/// </summary>
public static class NotebooksModule
{
    /// <summary>
    /// Adds Notebook feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNotebooksFeature(this IServiceCollection services)
    {
        services.AddScoped<CreateNotebookHandler>();
        services.AddScoped<ListNotebooksHandler>();
        services.AddScoped<GetNotebookHandler>();
        services.AddScoped<UpdateNotebookHandler>();
        services.AddScoped<DeleteNotebookHandler>();

        return services;
    }
}
