using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Prism.Common.Inference;
using Prism.Common.Inference.Runtime;
using Prism.Features.History.Application.GetRecord;
using Prism.Features.History.Application.ReplaySingle;
using Prism.Features.History.Application.SearchHistory;
using Prism.Features.History.Application.TagRecord;
using Prism.Common.Database.Seeders;
using Prism.Features.History.Infrastructure;

namespace Prism.Features.History;

/// <summary>
/// Dependency injection module for the History and Replay feature.
/// Registers the channel, background persistence service, and all handlers.
/// </summary>
public static class HistoryModule
{
    /// <summary>
    /// Registers all History feature services in the dependency injection container,
    /// including the <see cref="Channel{T}"/> for <see cref="InferenceRecordData"/> and
    /// the background persistence service.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHistoryFeature(this IServiceCollection services)
    {
        // Register the channel for async inference record persistence
        services.AddSingleton(Channel.CreateUnbounded<InferenceRecordData>(
            new UnboundedChannelOptions { SingleReader = true }));

        // Background service that consumes from the channel and writes to the database
        services.AddHostedService<InferenceRecordPersistenceService>();

        // Handlers
        services.AddScoped<SearchHistoryHandler>();
        services.AddScoped<GetRecordHandler>();
        services.AddScoped<TagRecordHandler>();
        services.AddScoped<ReplaySingleHandler>();

        // Replay service (implements IReplayService from Common)
        services.AddScoped<IReplayService, ReplayService>();

        // Seeders
        services.AddScoped<IDataSeeder, HistorySeeder>();

        return services;
    }
}
