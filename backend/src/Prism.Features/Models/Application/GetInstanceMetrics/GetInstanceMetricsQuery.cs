namespace Prism.Features.Models.Application.GetInstanceMetrics;

/// <summary>
/// Query to retrieve live performance metrics for a specific inference instance.
/// </summary>
/// <param name="InstanceId">The unique identifier of the instance to get metrics for.</param>
public sealed record GetInstanceMetricsQuery(Guid InstanceId);
