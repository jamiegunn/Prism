using Prism.Common.Inference.Capabilities;
using Prism.Common.Results;

namespace Prism.Features.Models.Application.GetCapabilities;

/// <summary>
/// Handles retrieving cached capability snapshots for provider instances.
/// </summary>
public sealed class GetCapabilitiesHandler
{
    private readonly IProviderCapabilityRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCapabilitiesHandler"/> class.
    /// </summary>
    /// <param name="registry">The capability registry for reading cached capabilities.</param>
    public GetCapabilitiesHandler(IProviderCapabilityRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Gets the cached capability snapshot for a specific provider instance.
    /// </summary>
    /// <param name="instanceId">The provider instance to look up.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the cached capability snapshot.</returns>
    public async Task<Result<ProviderCapabilitySnapshot>> HandleAsync(Guid instanceId, CancellationToken ct)
    {
        return await _registry.GetCachedAsync(instanceId, ct);
    }

    /// <summary>
    /// Gets capability snapshots for all registered provider instances.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A list of capability snapshots.</returns>
    public async Task<IReadOnlyList<ProviderCapabilitySnapshot>> HandleAllAsync(CancellationToken ct)
    {
        return await _registry.ListAllAsync(ct);
    }
}
