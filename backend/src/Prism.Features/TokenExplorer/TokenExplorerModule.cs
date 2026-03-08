using Microsoft.Extensions.DependencyInjection;
using Prism.Features.TokenExplorer.Application.ExploreBranch;
using Prism.Features.TokenExplorer.Application.PredictNextToken;
using Prism.Features.TokenExplorer.Application.StepThrough;
using Prism.Features.TokenExplorer.Application.Tokenize;

namespace Prism.Features.TokenExplorer;

/// <summary>
/// Dependency injection module for the Token Explorer feature.
/// Registers all handlers for next-token prediction, step-through, and branch exploration.
/// </summary>
public static class TokenExplorerModule
{
    /// <summary>
    /// Registers all Token Explorer feature services in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTokenExplorerFeature(this IServiceCollection services)
    {
        services.AddScoped<PredictNextTokenHandler>();
        services.AddScoped<StepThroughHandler>();
        services.AddScoped<ExploreBranchHandler>();
        services.AddScoped<TokenizeHandler>();
        services.AddScoped<CompareTokenizeHandler>();
        services.AddScoped<DetokenizeHandler>();

        return services;
    }
}
