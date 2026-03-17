using Microsoft.Extensions.DependencyInjection;

namespace Prism.Common.Inference.Runtime;

/// <summary>
/// Extension methods for registering inference runtime services in the dependency injection container.
/// </summary>
public static class RuntimeServiceExtensions
{
    /// <summary>
    /// Registers the inference runtime, token analysis service, recorder, and replay service.
    /// Should be called after the History feature (which registers the channel) and
    /// after the Models feature (which registers the provider factory).
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInferenceRuntime(this IServiceCollection services)
    {
        // Token analysis (stateless, singleton-safe)
        services.AddSingleton<ITokenAnalysisService, TokenAnalysisService>();

        // Recorder bridges to the existing channel-based persistence
        services.AddSingleton<IInferenceRecorder, ChannelInferenceRecorder>();

        // Runtime orchestrates everything
        services.AddScoped<IInferenceRuntime, InferenceRuntime>();

        // Note: IReplayService is registered in HistoryModule (it depends on feature entities)

        return services;
    }
}
