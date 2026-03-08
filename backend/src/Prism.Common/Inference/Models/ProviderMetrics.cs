namespace Prism.Common.Inference.Models;

/// <summary>
/// Represents runtime performance metrics from an inference provider.
/// Primarily populated from vLLM's Prometheus metrics endpoint.
/// </summary>
/// <param name="RequestsPerSecond">The number of requests processed per second.</param>
/// <param name="QueueDepth">The number of requests currently waiting in the queue.</param>
/// <param name="GpuUtilization">GPU utilization percentage (0-100).</param>
/// <param name="GpuMemoryUsed">GPU memory currently in use, in bytes.</param>
/// <param name="GpuMemoryTotal">Total GPU memory available, in bytes.</param>
/// <param name="KvCacheUtilization">KV cache utilization percentage (0-100).</param>
/// <param name="ActiveRequests">The number of requests currently being processed.</param>
public sealed record ProviderMetrics(
    double? RequestsPerSecond,
    int? QueueDepth,
    double? GpuUtilization,
    long? GpuMemoryUsed,
    long? GpuMemoryTotal,
    double? KvCacheUtilization,
    int? ActiveRequests);
