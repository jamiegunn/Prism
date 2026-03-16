using Microsoft.Extensions.DependencyInjection;
using Prism.Features.FineTuning.Application.CreateAdapter;
using Prism.Features.FineTuning.Application.DeleteAdapter;
using Prism.Features.FineTuning.Application.ExportDataset;
using Prism.Features.FineTuning.Application.ListAdapters;

namespace Prism.Features.FineTuning;

/// <summary>
/// Registers all Fine-Tuning services in the dependency injection container.
/// </summary>
public static class FineTuningModule
{
    /// <summary>
    /// Adds Fine-Tuning feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFineTuningFeature(this IServiceCollection services)
    {
        services.AddScoped<CreateAdapterHandler>();
        services.AddScoped<ListAdaptersHandler>();
        services.AddScoped<DeleteAdapterHandler>();
        services.AddScoped<ExportFineTuneHandler>();

        return services;
    }
}
