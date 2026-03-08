using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Application.Dtos;

/// <summary>
/// Data transfer object containing live performance metrics for an inference instance.
/// Combines data from the database with real-time metrics from the provider.
/// </summary>
/// <param name="InstanceId">The unique identifier of the instance.</param>
/// <param name="ModelId">The identifier of the currently loaded model.</param>
/// <param name="Status">The current operational status.</param>
/// <param name="GpuUtilization">GPU utilization percentage (0-100), if available.</param>
/// <param name="GpuMemoryUsed">GPU memory currently in use in bytes, if available.</param>
/// <param name="GpuMemoryTotal">Total GPU memory available in bytes, if available.</param>
/// <param name="KvCacheUtilization">KV cache utilization percentage (0-100), if available.</param>
/// <param name="ActiveRequests">The number of requests currently being processed, if available.</param>
/// <param name="RequestsPerSecond">The number of requests processed per second, if available.</param>
/// <param name="QueueDepth">The number of requests waiting in the queue, if available.</param>
public sealed record InstanceMetricsDto(
    Guid InstanceId,
    string? ModelId,
    InstanceStatus Status,
    double? GpuUtilization,
    long? GpuMemoryUsed,
    long? GpuMemoryTotal,
    double? KvCacheUtilization,
    int? ActiveRequests,
    double? RequestsPerSecond,
    int? QueueDepth);
