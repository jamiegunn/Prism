using Microsoft.Extensions.DependencyInjection;
using Prism.Features.Rag.Application.CreateCollection;
using Prism.Features.Rag.Application.DeleteCollection;
using Prism.Features.Rag.Application.GetCollection;
using Prism.Features.Rag.Application.GetCollectionStats;
using Prism.Features.Rag.Application.IngestDocument;
using Prism.Features.Rag.Application.ListCollections;
using Prism.Features.Rag.Application.ListDocuments;
using Prism.Features.Rag.Application.QueryCollection;
using Prism.Features.Rag.Application.RagPipeline;
using Prism.Features.Rag.Domain;
using Prism.Features.Rag.Domain.Chunking;
using Prism.Features.Rag.Domain.Parsing;
using Prism.Common.Database.Seeders;
using Prism.Features.Rag.Infrastructure;

namespace Prism.Features.Rag;

/// <summary>
/// Registers all RAG Workbench services in the dependency injection container.
/// </summary>
public static class RagModule
{
    /// <summary>
    /// Adds RAG feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRagFeature(this IServiceCollection services)
    {
        // Handlers
        services.AddScoped<CreateCollectionHandler>();
        services.AddScoped<ListCollectionsHandler>();
        services.AddScoped<GetCollectionHandler>();
        services.AddScoped<DeleteCollectionHandler>();
        services.AddScoped<IngestDocumentHandler>();
        services.AddScoped<ListDocumentsHandler>();
        services.AddScoped<QueryCollectionHandler>();
        services.AddScoped<RagPipelineHandler>();
        services.AddScoped<GetCollectionStatsHandler>();

        // Labelled query sets + retrieval evaluation
        services.AddScoped<Application.QuerySets.CreateQuerySetHandler>();
        services.AddScoped<Application.QuerySets.ListQuerySetsHandler>();
        services.AddScoped<Application.QuerySets.DeleteQuerySetHandler>();
        services.AddScoped<Application.QuerySets.EvaluateRetrievalHandler>();

        // Chunking strategies
        services.AddSingleton<IChunkingStrategy, FixedSizeChunker>();
        services.AddSingleton<IChunkingStrategy, SentenceChunker>();
        services.AddSingleton<IChunkingStrategy, RecursiveChunker>();

        // Document parsers
        services.AddSingleton<IDocumentParser, PlainTextParser>();
        services.AddSingleton<IDocumentParser, HtmlParser>();

        // Embedding provider
        // Scoped, not singleton. It resolves its endpoint from the registered inference
        // instances, which means it takes AppDbContext — and a singleton holding a scoped
        // DbContext is a captive dependency: never disposed, shared across every request, and
        // DbContext is not thread-safe. Only scoped handlers consume this, so scoped is both
        // correct and sufficient.
        services.AddScoped<IEmbeddingProvider, OpenAiEmbeddingProvider>();

        // Seeders
        services.AddScoped<RagSampleEmbedder>();
        services.AddScoped<IDataSeeder, RagSeeder>();

        // Seeding happens before anything has been health-checked, so a first launch often has no
        // reachable server yet. This tries again once there is one, which is the difference
        // between semantic search working on a fresh install and working after a restart.
        services.AddHostedService<RagSampleEmbeddingService>();

        return services;
    }
}
