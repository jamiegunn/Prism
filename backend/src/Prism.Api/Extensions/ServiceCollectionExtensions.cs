using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Prism.Common.Auth;
using Prism.Common.Auth.Providers;
using Prism.Common.Cache;
using Prism.Common.Cache.Providers;
using Prism.Common.Database;
using Prism.Common.Jobs;
using Prism.Common.Database.Interceptors;
using Prism.Common.Database.Seeders;
using Prism.Common.Storage;
using Prism.Common.Storage.Providers;
using StackExchange.Redis;
using Prism.Features.Experiments;
using Prism.Features.History;
using Prism.Features.PromptLab;
using Prism.Features.Models;
using Prism.Features.Playground;
using Prism.Features.Analytics;
using Prism.Features.BatchInference;
using Prism.Features.Datasets;
using Prism.Features.Evaluation;
using Prism.Features.Rag;
using Prism.Features.Agents;
using Prism.Features.FineTuning;
using Prism.Features.Notebooks;
using Prism.Features.StructuredOutput;
using Prism.Features.TokenExplorer;
using Prism.Features.Workspaces;
using Prism.Common.Inference.Runtime;

namespace Prism.Api.Extensions;

/// <summary>
/// Extension methods for configuring dependency injection on <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all common/shared services including database, cache, storage, auth, and inference providers.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCommonServices(this IServiceCollection services, IConfiguration config)
    {
        // Database — EF Core with Npgsql
        string connectionString = config.GetConnectionString("DefaultConnection")
            ?? config["Database:ConnectionString"]
            ?? throw new InvalidOperationException("Database connection string is not configured.");

        services.AddSingleton<AuditInterceptor>();
        services.AddSingleton<SoftDeleteInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.UseVector());
            options.AddInterceptors(
                sp.GetRequiredService<AuditInterceptor>(),
                sp.GetRequiredService<SoftDeleteInterceptor>());
        });

        // Register Features assembly for EF config scanning
        AppDbContext.RegisterAssembly(typeof(Prism.Features.Marker).Assembly);

        // Cache (config-driven)
        services.AddCommonCache(config);

        // Storage (config-driven)
        services.AddCommonStorage(config);

        // Auth (config-driven)
        services.AddCommonAuth(config);

        // Inference
        services.AddCommonInference(config);

        // Job queue (config-driven)
        services.AddCommonJobQueue(config);

        // Seed data runner
        services.AddSingleton<SeedDataRunner>();

        return services;
    }

    /// <summary>
    /// Registers feature-specific services. Called after common services are registered.
    /// Features are added here as they are implemented.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFeatureServices(this IServiceCollection services, IConfiguration config)
    {
        // Will be filled in as features are built
        services.AddWorkspacesFeature();
        services.AddPlaygroundFeature();
        services.AddModelsFeature();
        services.AddTokenExplorerFeature();
        services.AddHistoryFeature();
        services.AddExperimentsFeature();
        services.AddPromptLabFeature();
        services.AddDatasetsFeature();
        services.AddEvaluationFeature();
        services.AddBatchInferenceFeature();
        services.AddAnalyticsFeature();
        services.AddRagFeature();
        services.AddStructuredOutputFeature();
        services.AddAgentsFeature();
        services.AddFineTuningFeature();
        services.AddNotebooksFeature();

        // Inference runtime — must be registered after History (channel) and Models (factory)
        services.AddInferenceRuntime();

        return services;
    }

    /// <summary>
    /// Registers the cache service based on the configured provider.
    /// Supports InMemory (default), Redis, and None (no-op).
    /// </summary>
    private static IServiceCollection AddCommonCache(this IServiceCollection services, IConfiguration config)
    {
        string cacheProvider = config["Cache:Provider"] ?? "InMemory";

        switch (cacheProvider)
        {
            case "None":
                services.AddSingleton<ICacheService, NullCacheService>();
                break;
            case "Redis":
                AddRedisConnection(services, config);
                services.AddSingleton<ICacheService, RedisCacheService>();
                break;
            case "InMemory":
            default:
                services.AddMemoryCache();
                services.AddSingleton<ICacheService, InMemoryCacheService>();
                break;
        }

        return services;
    }

    /// <summary>
    /// Registers the file storage service based on the configured provider.
    /// Supports Local filesystem (default) with Azure Blob and S3 planned for later phases.
    /// </summary>
    private static IServiceCollection AddCommonStorage(this IServiceCollection services, IConfiguration config)
    {
        string storageProvider = config["Storage:Provider"] ?? "Local";

        switch (storageProvider)
        {
            case "None":
                services.AddSingleton<IFileStorage, NullFileStorage>();
                break;
            case "Local":
            default:
                string basePath = config["Storage:Local:BasePath"] ?? "./data/storage";
                services.AddSingleton<IFileStorage>(sp =>
                    new LocalFileStorage(basePath, sp.GetRequiredService<ILogger<LocalFileStorage>>()));
                break;
        }

        return services;
    }

    /// <summary>
    /// Registers the authentication provider based on configuration.
    /// Defaults to NoAuth for single-user mode. Supports future Entra/OIDC providers.
    /// </summary>
    private static IServiceCollection AddCommonAuth(this IServiceCollection services, IConfiguration config)
    {
        string authProvider = config["Auth:Provider"] ?? "None";

        switch (authProvider)
        {
            case "None":
            default:
                services.AddSingleton<IAuthProvider, NoAuthProvider>();
                services.AddScoped<ICurrentUser>(sp =>
                {
                    CurrentUser user = new();
                    user.SetFromUserInfo(new UserInfo("local-user", "Local User", "local@prism.dev", new[] { "admin", "user" }));
                    return user;
                });
                break;
        }

        return services;
    }

    /// <summary>
    /// Registers inference provider services based on configuration.
    /// HttpClient is registered for use by inference providers.
    /// </summary>
    private static IServiceCollection AddCommonInference(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpClient();
        return services;
    }

    /// <summary>
    /// Registers the job queue service based on the configured provider.
    /// Supports InMemory (default) and Redis.
    /// </summary>
    private static IServiceCollection AddCommonJobQueue(this IServiceCollection services, IConfiguration config)
    {
        string jobQueueProvider = config["JobQueue:Provider"] ?? "InMemory";

        switch (jobQueueProvider)
        {
            case "Redis":
                AddRedisConnection(services, config);
                services.AddSingleton<IJobQueue, RedisJobQueue>();
                break;
            case "InMemory":
            default:
                services.AddSingleton<IJobQueue, InMemoryJobQueue>();
                break;
        }

        return services;
    }

    /// <summary>
    /// Registers the shared Redis <see cref="IConnectionMultiplexer"/> singleton if not already registered.
    /// Both cache and job queue can share the same connection.
    /// </summary>
    private static void AddRedisConnection(IServiceCollection services, IConfiguration config)
    {
        // Only register once — both cache and job queue share the same connection
        if (services.Any(s => s.ServiceType == typeof(IConnectionMultiplexer)))
        {
            return;
        }

        string connectionString = config["Redis:ConnectionString"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(connectionString));
    }
}
