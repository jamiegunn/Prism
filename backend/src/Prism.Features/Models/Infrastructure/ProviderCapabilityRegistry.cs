using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Inference.Capabilities;
using Prism.Common.Inference.Models;
using Prism.Common.Results;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Infrastructure;

/// <summary>
/// Probes provider instances for their actual capabilities by making test API calls,
/// persists the results on the <see cref="InferenceInstance"/> entity, and serves
/// cached capability snapshots to features and UI.
/// </summary>
public sealed class ProviderCapabilityRegistry : IProviderCapabilityRegistry
{
    private readonly AppDbContext _context;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<ProviderCapabilityRegistry> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCapabilityRegistry"/> class.
    /// </summary>
    /// <param name="context">The database context for reading and updating instance records.</param>
    /// <param name="providerFactory">The factory for creating provider instances to probe.</param>
    /// <param name="logger">The logger for probe operations.</param>
    public ProviderCapabilityRegistry(
        AppDbContext context,
        InferenceProviderFactory providerFactory,
        ILogger<ProviderCapabilityRegistry> logger)
    {
        _context = context;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ProviderCapabilitySnapshot>> ProbeAsync(Guid instanceId, CancellationToken ct)
    {
        InferenceInstance? instance = await _context.Set<InferenceInstance>()
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct);

        if (instance is null)
        {
            return Result<ProviderCapabilitySnapshot>.Failure(
                Error.NotFound($"Inference instance {instanceId} not found."));
        }

        _logger.LogInformation(
            "Probing capabilities for instance {InstanceName} ({ProviderType}) at {Endpoint}",
            instance.Name, instance.ProviderType, instance.Endpoint);

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        ProviderCapabilitySnapshot snapshot = await ProbeProvider(instance, provider, ct);

        // Persist probed capabilities back to the instance
        ApplyToInstance(instance, snapshot);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Probe complete for {InstanceName}: tier={Tier}, logprobs={Logprobs}, tokenize={Tokenize}, guided={Guided}",
            instance.Name, snapshot.Tier, snapshot.SupportsLogprobs, snapshot.SupportsTokenize, snapshot.SupportsGuidedDecoding);

        return Result<ProviderCapabilitySnapshot>.Success(snapshot);
    }

    /// <inheritdoc />
    public async Task<Result<ProviderCapabilitySnapshot>> GetCachedAsync(Guid instanceId, CancellationToken ct)
    {
        InferenceInstance? instance = await _context.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct);

        if (instance is null)
        {
            return Result<ProviderCapabilitySnapshot>.Failure(
                Error.NotFound($"Inference instance {instanceId} not found."));
        }

        return Result<ProviderCapabilitySnapshot>.Success(SnapshotFromInstance(instance));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProviderCapabilitySnapshot>> ListAllAsync(CancellationToken ct)
    {
        List<InferenceInstance> instances = await _context.Set<InferenceInstance>()
            .AsNoTracking()
            .ToListAsync(ct);

        return instances.Select(SnapshotFromInstance).ToList();
    }

    /// <summary>
    /// Probes a provider by checking health, model info, and attempting tokenization.
    /// </summary>
    private async Task<ProviderCapabilitySnapshot> ProbeProvider(
        InferenceInstance instance,
        IInferenceProvider provider,
        CancellationToken ct)
    {
        DateTime probedAt = DateTime.UtcNow;

        // Start with the provider's declared capabilities
        ProviderCapabilities declared = provider.Capabilities;

        bool healthOk = false;
        bool tokenizeWorks = false;
        int maxLogprobs = declared.MaxTopLogprobs;
        bool metricsWork = false;
        string? probeError = null;

        // 1. Health check
        try
        {
            Result<HealthStatus> healthResult = await provider.CheckHealthAsync(ct);
            healthOk = healthResult.IsSuccess && healthResult.Value.IsHealthy;
        }
        catch (Exception ex)
        {
            probeError = $"Health check failed: {ex.Message}";
            _logger.LogWarning(ex, "Health check failed for {InstanceName}", instance.Name);
        }

        // 2. Tokenize probe (if declared)
        if (declared.SupportsTokenize)
        {
            try
            {
                Result<TokenizeResponse> tokenizeResult = await provider.TokenizeAsync("Hello world", ct);
                tokenizeWorks = tokenizeResult.IsSuccess && tokenizeResult.Value.TokenCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Tokenize probe failed for {InstanceName}", instance.Name);
            }
        }

        // 3. Metrics probe (if declared)
        if (declared.SupportsMetrics)
        {
            try
            {
                Result<ProviderMetrics> metricsResult = await provider.GetMetricsAsync(ct);
                metricsWork = metricsResult.IsSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Metrics probe failed for {InstanceName}", instance.Name);
            }
        }

        // Build the verified snapshot
        bool supportsLogprobs = declared.SupportsLogprobs;
        bool supportsStreaming = declared.SupportsStreaming;
        bool supportsGuidedDecoding = declared.SupportsGuidedDecoding;
        bool supportsModelSwap = declared.SupportsHotReload;

        CapabilityTier tier = ComputeTier(
            supportsLogprobs, tokenizeWorks, supportsGuidedDecoding, metricsWork);

        return new ProviderCapabilitySnapshot
        {
            InstanceId = instance.Id,
            ProviderName = instance.Name,
            Tier = tier,
            SupportsLogprobs = supportsLogprobs,
            MaxLogprobs = maxLogprobs,
            SupportsTokenize = tokenizeWorks,
            SupportsGuidedDecoding = supportsGuidedDecoding,
            SupportsStreaming = supportsStreaming,
            SupportsFunctionCalling = false,
            SupportsMetrics = metricsWork,
            SupportsModelSwap = supportsModelSwap,
            SupportsMultimodal = false,
            ProbedAt = probedAt,
            ProbeSucceeded = healthOk || probeError is null,
            ProbeError = probeError
        };
    }

    /// <summary>
    /// Computes the capability tier from individual capability flags.
    /// </summary>
    private static CapabilityTier ComputeTier(
        bool logprobs, bool tokenize, bool guidedDecoding, bool metrics)
    {
        if (logprobs && tokenize && guidedDecoding && metrics)
            return CapabilityTier.Research;

        if (logprobs || tokenize)
            return CapabilityTier.Inspect;

        return CapabilityTier.Chat;
    }

    /// <summary>
    /// Updates the instance entity with probed capability values.
    /// </summary>
    private static void ApplyToInstance(InferenceInstance instance, ProviderCapabilitySnapshot snapshot)
    {
        instance.SupportsLogprobs = snapshot.SupportsLogprobs;
        instance.MaxTopLogprobs = snapshot.MaxLogprobs;
        instance.SupportsTokenize = snapshot.SupportsTokenize;
        instance.SupportsGuidedDecoding = snapshot.SupportsGuidedDecoding;
        instance.SupportsStreaming = snapshot.SupportsStreaming;
        instance.SupportsMetrics = snapshot.SupportsMetrics;
        instance.SupportsModelSwap = snapshot.SupportsModelSwap;
        instance.SupportsMultimodal = snapshot.SupportsMultimodal;
        instance.LastHealthCheck = snapshot.ProbedAt;
        instance.LastHealthError = snapshot.ProbeError;
    }

    /// <summary>
    /// Builds a snapshot from the persisted instance data (without probing).
    /// </summary>
    private static ProviderCapabilitySnapshot SnapshotFromInstance(InferenceInstance instance)
    {
        CapabilityTier tier = ComputeTier(
            instance.SupportsLogprobs,
            instance.SupportsTokenize,
            instance.SupportsGuidedDecoding,
            instance.SupportsMetrics);

        return new ProviderCapabilitySnapshot
        {
            InstanceId = instance.Id,
            ProviderName = instance.Name,
            Tier = tier,
            SupportsLogprobs = instance.SupportsLogprobs,
            MaxLogprobs = instance.MaxTopLogprobs,
            SupportsTokenize = instance.SupportsTokenize,
            SupportsGuidedDecoding = instance.SupportsGuidedDecoding,
            SupportsStreaming = instance.SupportsStreaming,
            SupportsMetrics = instance.SupportsMetrics,
            SupportsModelSwap = instance.SupportsModelSwap,
            SupportsMultimodal = instance.SupportsMultimodal,
            ProbedAt = instance.LastHealthCheck ?? DateTime.MinValue,
            ProbeSucceeded = instance.LastHealthError is null,
            ProbeError = instance.LastHealthError
        };
    }
}
