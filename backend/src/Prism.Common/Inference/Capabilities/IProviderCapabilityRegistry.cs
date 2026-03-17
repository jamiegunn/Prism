using Prism.Common.Results;

namespace Prism.Common.Inference.Capabilities;

/// <summary>
/// Probes, caches, and serves provider capability information.
/// Features and UI read capabilities to determine which controls to enable.
/// </summary>
public interface IProviderCapabilityRegistry
{
    /// <summary>
    /// Probes a provider instance to discover its actual capabilities.
    /// Updates the persisted capability data on the instance.
    /// </summary>
    /// <param name="instanceId">The provider instance to probe.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the probed capability snapshot.</returns>
    Task<Result<ProviderCapabilitySnapshot>> ProbeAsync(Guid instanceId, CancellationToken ct);

    /// <summary>
    /// Gets the cached capability snapshot for a provider instance.
    /// Returns the last probed result without making network calls.
    /// </summary>
    /// <param name="instanceId">The provider instance to look up.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the cached snapshot, or an error if never probed.</returns>
    Task<Result<ProviderCapabilitySnapshot>> GetCachedAsync(Guid instanceId, CancellationToken ct);

    /// <summary>
    /// Gets capability snapshots for all registered provider instances.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A list of capability snapshots for all known instances.</returns>
    Task<IReadOnlyList<ProviderCapabilitySnapshot>> ListAllAsync(CancellationToken ct);
}
