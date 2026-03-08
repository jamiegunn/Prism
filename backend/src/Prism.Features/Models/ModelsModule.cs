using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Prism.Features.Models.Application;
using Prism.Features.Models.Application.CheckHealth;
using Prism.Features.Models.Application.GetInstanceMetrics;
using Prism.Features.Models.Application.ListInstances;
using Prism.Features.Models.Application.RegisterInstance;
using Prism.Features.Models.Application.SwapModel;
using Prism.Features.Models.Application.GetTokenizerInfo;
using Prism.Features.Models.Application.UnregisterInstance;
using Prism.Features.Models.Infrastructure;

namespace Prism.Features.Models;

/// <summary>
/// Dependency injection module for the Models feature.
/// Registers all handlers, validators, and background services.
/// </summary>
public static class ModelsModule
{
    /// <summary>
    /// Registers all Models feature services in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddModelsFeature(this IServiceCollection services)
    {
        // Provider factory
        services.AddSingleton<InferenceProviderFactory>();

        // Handlers
        services.AddScoped<RegisterInstanceHandler>();
        services.AddScoped<UnregisterInstanceHandler>();
        services.AddScoped<ListInstancesHandler>();
        services.AddScoped<GetInstanceMetricsHandler>();
        services.AddScoped<SwapModelHandler>();
        services.AddScoped<CheckHealthHandler>();
        services.AddScoped<GetTokenizerInfoHandler>();

        // Validators
        services.AddScoped<IValidator<RegisterInstanceCommand>, RegisterInstanceValidator>();

        // Background services
        services.AddHostedService<HealthCheckBackgroundService>();

        return services;
    }
}
