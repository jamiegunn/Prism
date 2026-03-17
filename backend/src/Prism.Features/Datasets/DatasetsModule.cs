using Microsoft.Extensions.DependencyInjection;
using Prism.Features.Datasets.Application.DeleteDataset;
using Prism.Features.Datasets.Application.ExportDataset;
using Prism.Features.Datasets.Application.GetDataset;
using Prism.Features.Datasets.Application.GetDatasetStats;
using Prism.Features.Datasets.Application.ListDatasets;
using Prism.Features.Datasets.Application.ListRecords;
using Prism.Features.Datasets.Application.SplitDataset;
using Prism.Features.Datasets.Application.UpdateDataset;
using Prism.Features.Datasets.Application.UpdateRecord;
using Prism.Common.Database.Seeders;
using Prism.Features.Datasets.Application.UploadDataset;
using Prism.Features.Datasets.Application.ValidateDataset;
using Prism.Features.Datasets.Application.AnnotateRecord;
using Prism.Features.Datasets.Infrastructure;

namespace Prism.Features.Datasets;

/// <summary>
/// Dependency injection module for the Datasets feature.
/// Registers all handlers.
/// </summary>
public static class DatasetsModule
{
    /// <summary>
    /// Registers all Datasets feature services in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDatasetsFeature(this IServiceCollection services)
    {
        services.AddScoped<UploadDatasetHandler>();
        services.AddScoped<ListDatasetsHandler>();
        services.AddScoped<GetDatasetHandler>();
        services.AddScoped<UpdateDatasetHandler>();
        services.AddScoped<DeleteDatasetHandler>();
        services.AddScoped<ListRecordsHandler>();
        services.AddScoped<UpdateRecordHandler>();
        services.AddScoped<SplitDatasetHandler>();
        services.AddScoped<GetDatasetStatsHandler>();
        services.AddScoped<ExportDatasetHandler>();
        services.AddScoped<ValidateDatasetHandler>();
        services.AddScoped<AnnotateRecordHandler>();

        // Seeders
        services.AddScoped<IDataSeeder, DatasetsSeeder>();

        return services;
    }
}
