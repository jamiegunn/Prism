namespace Prism.Features.Models.Application.CheckHealth;

/// <summary>
/// Command to trigger a health check on a specific inference instance.
/// </summary>
/// <param name="InstanceId">The unique identifier of the instance to check.</param>
public sealed record CheckHealthCommand(Guid InstanceId);
