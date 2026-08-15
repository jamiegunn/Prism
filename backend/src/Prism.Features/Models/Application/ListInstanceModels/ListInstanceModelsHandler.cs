using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Results;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Application.ListInstanceModels;

/// <summary>
/// Query for the models an instance can currently serve.
/// </summary>
/// <param name="InstanceId">The instance to ask.</param>
public sealed record ListInstanceModelsQuery(Guid InstanceId);

/// <summary>
/// The models an instance serves, or the stated reason the list is unavailable.
/// </summary>
/// <param name="Models">Model identifiers the server reports, in the order it reports them.</param>
/// <param name="CanList">
/// Whether this provider can enumerate its models at all. False is not a failure — a vLLM
/// process serves the one model it was started with — and a client should offer free text
/// rather than an empty menu.
/// </param>
/// <param name="Reason">Why the list is empty or unavailable, when it is; otherwise null.</param>
/// <param name="EmbeddingOnly">
/// The subset of <paramref name="Models"/> that can only produce embeddings, so a picker can
/// keep them out of reach. An instance is asked to hold conversations, and choosing one of these
/// leaves it unable to answer anything — the state the health check spends its time repairing.
/// Empty when the server does not say what its models are for.
/// </param>
public sealed record InstanceModelsDto(
    IReadOnlyList<string> Models,
    bool CanList,
    string? Reason,
    IReadOnlyList<string> EmbeddingOnly);

/// <summary>
/// Asks an instance which models it can serve.
/// </summary>
/// <remarks>
/// Added for replay, where the model is part of the request being re-run: replaying a record
/// against an instance that does not serve its model failed with "model not found" and left the
/// reader to type an exact identifier from memory. Nothing else in the app could answer "what
/// does this server have" either — the model-swap box asks you to type it too.
/// </remarks>
public sealed class ListInstanceModelsHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<ListInstanceModelsHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListInstanceModelsHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="providerFactory">The factory for creating inference provider instances.</param>
    /// <param name="logger">The logger instance.</param>
    public ListInstanceModelsHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ILogger<ListInstanceModelsHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Lists the models the instance reports.
    /// </summary>
    /// <param name="query">The query naming the instance.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The models, or the reason there are none to show.</returns>
    public async Task<Result<InstanceModelsDto>> HandleAsync(ListInstanceModelsQuery query, CancellationToken ct)
    {
        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == query.InstanceId, ct);

        if (instance is null)
        {
            return Error.NotFound($"Inference instance with ID '{query.InstanceId}' was not found.");
        }

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        IHotReloadableProvider? hotReloadable = provider.As<IHotReloadableProvider>();

        if (hotReloadable is null)
        {
            // Not an error: this server serves what it was started with, and the instance
            // record already names it.
            return new InstanceModelsDto(
                instance.ModelId is { Length: > 0 } single ? [single] : [],
                CanList: false,
                Reason: $"A {instance.ProviderType} server serves the model it was started with; it cannot list others.",
                EmbeddingOnly: []);
        }

        Result<IReadOnlyList<AvailableModel>> models = await hotReloadable.ListAvailableModelsAsync(ct);

        if (models.IsFailure)
        {
            _logger.LogWarning(
                "Could not list models on instance {InstanceId}: {Error}",
                query.InstanceId, models.Error.Message);

            return new InstanceModelsDto([], CanList: true, Reason: models.Error.Message, EmbeddingOnly: []);
        }

        // Asked once, when the picker opens, against a server that is almost always local. A
        // provider that cannot say returns null and the model is offered as usual — silence is
        // not evidence that a model is embedding-only.
        IModelPurposeProbe? probe = provider.As<IModelPurposeProbe>();
        List<string> embeddingOnly = [];

        if (probe is not null)
        {
            foreach (AvailableModel model in models.Value)
            {
                if (await probe.CanGenerateTextAsync(model.ModelId, ct) is false)
                {
                    embeddingOnly.Add(model.ModelId);
                }
            }
        }

        return new InstanceModelsDto(
            [.. models.Value.Select(m => m.ModelId)],
            CanList: true,
            Reason: models.Value.Count == 0 ? "This server has no models pulled yet." : null,
            EmbeddingOnly: embeddingOnly);
    }
}
