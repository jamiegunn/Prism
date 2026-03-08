using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Results;
using Prism.Features.Models.Application.Dtos;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Application.GetInstanceMetrics;

/// <summary>
/// Handles retrieval of live performance metrics for an inference instance.
/// Combines database state with real-time metrics from the provider.
/// </summary>
public sealed class GetInstanceMetricsHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<GetInstanceMetricsHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetInstanceMetricsHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="providerFactory">The factory for creating inference provider instances.</param>
    /// <param name="logger">The logger instance.</param>
    public GetInstanceMetricsHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ILogger<GetInstanceMetricsHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves live metrics for the specified inference instance by querying the provider endpoint.
    /// Falls back to database-only data if the provider does not support metrics.
    /// </summary>
    /// <param name="query">The query containing the instance ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the instance metrics DTO on success.</returns>
    public async Task<Result<InstanceMetricsDto>> HandleAsync(GetInstanceMetricsQuery query, CancellationToken ct)
    {
        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == query.InstanceId, ct);

        if (instance is null)
        {
            return Error.NotFound($"Inference instance with ID '{query.InstanceId}' was not found.");
        }

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        double? gpuUtilization = null;
        long? gpuMemoryUsed = null;
        long? gpuMemoryTotal = null;
        double? kvCacheUtilization = null;
        int? activeRequests = null;
        double? requestsPerSecond = null;
        int? queueDepth = null;

        try
        {
            Result<ProviderMetrics> metricsResult = await provider.GetMetricsAsync(ct);
            if (metricsResult.IsSuccess)
            {
                ProviderMetrics metrics = metricsResult.Value;
                gpuUtilization = metrics.GpuUtilization;
                gpuMemoryUsed = metrics.GpuMemoryUsed;
                gpuMemoryTotal = metrics.GpuMemoryTotal;
                kvCacheUtilization = metrics.KvCacheUtilization;
                activeRequests = metrics.ActiveRequests;
                requestsPerSecond = metrics.RequestsPerSecond;
                queueDepth = metrics.QueueDepth;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get live metrics for instance {InstanceId}", query.InstanceId);
        }

        return new InstanceMetricsDto(
            InstanceId: instance.Id,
            ModelId: instance.ModelId,
            Status: instance.Status,
            GpuUtilization: gpuUtilization,
            GpuMemoryUsed: gpuMemoryUsed,
            GpuMemoryTotal: gpuMemoryTotal,
            KvCacheUtilization: kvCacheUtilization,
            ActiveRequests: activeRequests,
            RequestsPerSecond: requestsPerSecond,
            QueueDepth: queueDepth);
    }
}
