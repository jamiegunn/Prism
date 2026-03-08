using Microsoft.Extensions.DependencyInjection;
using Prism.Features.Analytics.Application.GetPerformance;
using Prism.Features.Analytics.Application.GetUsage;

namespace Prism.Features.Analytics;

/// <summary>
/// Dependency injection module for the Analytics feature.
/// Registers all handlers.
/// </summary>
public static class AnalyticsModule
{
    /// <summary>
    /// Registers all Analytics feature services in the dependency injection container.
    /// </summary>
    public static IServiceCollection AddAnalyticsFeature(this IServiceCollection services)
    {
        services.AddScoped<GetUsageHandler>();
        services.AddScoped<GetPerformanceHandler>();

        return services;
    }
}
