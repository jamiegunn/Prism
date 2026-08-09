using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Results;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Application.DiscoverProviders;

/// <summary>
/// A provider found running on this machine.
/// </summary>
/// <param name="ProviderType">The kind of server that answered.</param>
/// <param name="Endpoint">Where it answered.</param>
/// <param name="SuggestedName">A sensible default name for registration.</param>
/// <param name="Models">Model identifiers it reports, empty when it reports none.</param>
/// <param name="SupportsLogprobs">
/// Whether this provider can return per-token probabilities — the thing the heatmap, entropy
/// chart, surprise highlighting and Token Explorer are all built from.
/// </param>
/// <param name="AlreadyRegistered">Whether Prism already knows about this endpoint.</param>
/// <param name="Note">A plain-language remark about what this provider will and will not do.</param>
public sealed record DiscoveredProvider(
    InferenceProviderType ProviderType,
    string Endpoint,
    string SuggestedName,
    IReadOnlyList<string> Models,
    bool SupportsLogprobs,
    bool AlreadyRegistered,
    string Note);

/// <summary>
/// The result of looking for local inference servers.
/// </summary>
/// <param name="Found">Providers that answered.</param>
/// <param name="Probed">Every endpoint that was tried, so the UI can say what it looked for.</param>
public sealed record ProviderDiscoveryResult(
    IReadOnlyList<DiscoveredProvider> Found,
    IReadOnlyList<string> Probed);

/// <summary>
/// Looks for inference servers running on the conventional local ports.
/// </summary>
/// <remarks>
/// Exists because the first-run experience was: an empty instance list, and no indication of
/// what to do about it. A researcher who has just started Ollama should not have to know which
/// port it uses or which of four provider types to pick from a dropdown.
///
/// This runs server-side rather than in the browser because a page served from the Vite dev
/// server cannot probe <c>localhost:11434</c> — the cross-origin request is blocked before any
/// answer comes back.
/// </remarks>
public sealed class DiscoverProvidersHandler
{
    /// <summary>
    /// The conventional local endpoints probed at discovery time.
    /// </summary>
    /// <remarks>
    /// <c>dev.sh</c> keeps the same list in <c>PRISM_PROVIDER_CANDIDATES</c> so the launcher
    /// offers what the app can find. Internal rather than private so the parity test can hold
    /// the two in step; they were silently different before, and the launcher could not offer
    /// LM Studio even though the platform supported it.
    /// </remarks>
    internal static readonly (InferenceProviderType Type, string Endpoint, string Name)[] Candidates =
    [
        (InferenceProviderType.Vllm, "http://localhost:8000", "Local vLLM"),
        (InferenceProviderType.Ollama, "http://localhost:11434", "Local Ollama"),
        (InferenceProviderType.LmStudio, "http://localhost:1234/v1", "Local LM Studio"),
    ];

    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<DiscoverProvidersHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoverProvidersHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="providerFactory">Factory for inference providers.</param>
    /// <param name="logger">The logger instance.</param>
    public DiscoverProvidersHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ILogger<DiscoverProvidersHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Probes the conventional local ports and reports what answered.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>What was found, and what was looked for.</returns>
    public async Task<Result<ProviderDiscoveryResult>> HandleAsync(CancellationToken ct)
    {
        HashSet<string> registered = (await _db.Set<InferenceInstance>()
                .AsNoTracking()
                .Select(i => i.Endpoint)
                .ToListAsync(ct))
            .Select(Normalise)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Probed together: three sequential timeouts against nothing would make an empty
        // machine feel broken rather than empty.
        DiscoveredProvider?[] results = await Task.WhenAll(
            Candidates.Select(c => ProbeAsync(c.Type, c.Endpoint, c.Name, registered, ct)));

        return new ProviderDiscoveryResult(
            [.. results.OfType<DiscoveredProvider>()],
            [.. Candidates.Select(c => c.Endpoint)]);
    }

    /// <summary>
    /// Describes what a provider will and will not do, in terms of what the user came for.
    /// </summary>
    /// <param name="providerType">The provider kind.</param>
    /// <param name="supportsLogprobs">Whether it returns per-token probabilities.</param>
    /// <returns>A plain-language note.</returns>
    internal static string DescribeProvider(InferenceProviderType providerType, bool supportsLogprobs)
    {
        if (supportsLogprobs)
        {
            return providerType == InferenceProviderType.Vllm
                ? "Full introspection: token heatmaps, entropy, next-token exploration and guided decoding all work."
                : "Returns per-token probabilities, so the heatmap and entropy views will work.";
        }

        return providerType switch
        {
            InferenceProviderType.Ollama =>
                "Chat and structured output work. This Ollama predates 0.12.11, so it returns no "
                + "per-token probabilities and the heatmap, entropy chart and Token Explorer will "
                + "be empty. Updating Ollama is enough to turn them on.",
            _ =>
                "Chat works. This provider does not return per-token probabilities, so the "
                + "heatmap, entropy chart and Token Explorer will be empty.",
        };
    }

    private async Task<DiscoveredProvider?> ProbeAsync(
        InferenceProviderType type,
        string endpoint,
        string name,
        HashSet<string> registered,
        CancellationToken ct)
    {
        try
        {
            IInferenceProvider provider = _providerFactory.CreateProvider(name, endpoint, type);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));

            Result<HealthStatus> health = await provider.CheckHealthAsync(timeout.Token);

            if (health.IsFailure || !health.Value.IsHealthy)
            {
                return null;
            }

            Result<ModelInfo> model = await provider.GetModelInfoAsync(timeout.Token);
            bool supportsLogprobs = provider.Capabilities.SupportsLogprobs;

            List<string> models = model.IsSuccess && !string.IsNullOrWhiteSpace(model.Value.ModelId)
                ? [model.Value.ModelId]
                : health.Value.Model is { Length: > 0 } fromHealth ? [fromHealth] : [];

            return new DiscoveredProvider(
                type,
                endpoint,
                name,
                models,
                supportsLogprobs,
                registered.Contains(Normalise(endpoint)),
                DescribeProvider(type, supportsLogprobs));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Nothing listening is the common case, not an error worth surfacing.
            _logger.LogDebug(ex, "No {ProviderType} found at {Endpoint}", type, endpoint);
            return null;
        }
    }

    private static string Normalise(string endpoint) => endpoint.TrimEnd('/');
}
