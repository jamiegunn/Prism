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

        // Chunking strategies
        services.AddSingleton<IChunkingStrategy, FixedSizeChunker>();
        services.AddSingleton<IChunkingStrategy, SentenceChunker>();
        services.AddSingleton<IChunkingStrategy, RecursiveChunker>();

        // Document parsers
        services.AddSingleton<IDocumentParser, PlainTextParser>();
        services.AddSingleton<IDocumentParser, HtmlParser>();

        // Embedding provider
        services.AddSingleton<IEmbeddingProvider, OpenAiEmbeddingProvider>();

        // Seeders
        services.AddScoped<IDataSeeder, RagSeeder>();

        return services;
    }
}
