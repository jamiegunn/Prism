namespace Prism.Common.Inference.Models;

/// <summary>
/// Represents the health status of an inference provider.
/// </summary>
/// <param name="IsHealthy">Whether the provider is currently healthy and accepting requests.</param>
/// <param name="ProviderName">The name of the provider.</param>
/// <param name="Endpoint">The provider's endpoint URL.</param>
/// <param name="Model">The currently loaded model, if any.</param>
/// <param name="LastCheckAt">The UTC timestamp of the last health check.</param>
/// <param name="ErrorMessage">An error message if the provider is unhealthy.</param>
public sealed record HealthStatus(
    bool IsHealthy,
    string ProviderName,
    string Endpoint,
    string? Model,
    DateTime LastCheckAt,
    string? ErrorMessage);
