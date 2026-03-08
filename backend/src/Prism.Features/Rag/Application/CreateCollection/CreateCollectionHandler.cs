using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Rag.Application.Dtos;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Application.CreateCollection;

/// <summary>
/// Command to create a new RAG collection.
/// </summary>
public sealed record CreateCollectionCommand(
    string Name,
    string? Description,
    string EmbeddingModel,
    int Dimensions,
    string? DistanceMetric,
    string? ChunkingStrategy,
    int? ChunkSize,
    int? ChunkOverlap,
    Guid? ProjectId);

/// <summary>
/// Handles creation of a new RAG collection.
/// </summary>
public sealed class CreateCollectionHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreateCollectionHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateCollectionHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public CreateCollectionHandler(AppDbContext db, ILogger<CreateCollectionHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new RAG collection.
    /// </summary>
    /// <param name="command">The creation command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the created collection DTO.</returns>
    public async Task<Result<RagCollectionDto>> HandleAsync(CreateCollectionCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Error.Validation("Collection name is required.");

        if (string.IsNullOrWhiteSpace(command.EmbeddingModel))
            return Error.Validation("Embedding model is required.");

        if (command.Dimensions <= 0)
            return Error.Validation("Dimensions must be positive.");

        DistanceMetricType metric = Enum.TryParse<DistanceMetricType>(command.DistanceMetric, true, out DistanceMetricType parsed)
            ? parsed
            : DistanceMetricType.Cosine;

        var collection = new RagCollection
        {
            Name = command.Name,
            Description = command.Description,
            EmbeddingModel = command.EmbeddingModel,
            Dimensions = command.Dimensions,
            DistanceMetric = metric,
            ChunkingStrategy = command.ChunkingStrategy ?? "recursive",
            ChunkSize = command.ChunkSize ?? 512,
            ChunkOverlap = command.ChunkOverlap ?? 50,
            ProjectId = command.ProjectId
        };

        _db.Set<RagCollection>().Add(collection);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created RAG collection {CollectionName} with {Dimensions}d {Model} embeddings",
            collection.Name, collection.Dimensions, collection.EmbeddingModel);

        return RagCollectionDto.FromEntity(collection);
    }
}
