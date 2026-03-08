using Microsoft.Extensions.DependencyInjection;
using Prism.Features.BatchInference.Application.CreateBatchJob;
using Prism.Features.BatchInference.Application.DownloadBatchResults;
using Prism.Features.BatchInference.Application.EstimateBatchCost;
using Prism.Features.BatchInference.Application.GetBatchJob;
using Prism.Features.BatchInference.Application.GetBatchResults;
using Prism.Features.BatchInference.Application.ListBatchJobs;
using Prism.Features.BatchInference.Application.RetryFailed;
using Prism.Features.BatchInference.Application.UpdateBatchJobStatus;

namespace Prism.Features.BatchInference;

/// <summary>
/// Dependency injection module for the Batch Inference feature.
/// Registers all handlers.
/// </summary>
public static class BatchInferenceModule
{
    /// <summary>
    /// Registers all Batch Inference feature services in the dependency injection container.
    /// </summary>
    public static IServiceCollection AddBatchInferenceFeature(this IServiceCollection services)
    {
        services.AddScoped<CreateBatchJobHandler>();
        services.AddScoped<ListBatchJobsHandler>();
        services.AddScoped<GetBatchJobHandler>();
        services.AddScoped<UpdateBatchJobStatusHandler>();
        services.AddScoped<GetBatchResultsHandler>();
        services.AddScoped<DownloadBatchResultsHandler>();
        services.AddScoped<RetryFailedHandler>();
        services.AddScoped<EstimateBatchCostHandler>();

        return services;
    }
}
