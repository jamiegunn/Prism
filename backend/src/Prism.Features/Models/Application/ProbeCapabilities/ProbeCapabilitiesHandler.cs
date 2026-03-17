using Prism.Common.Inference.Capabilities;
using Prism.Common.Results;

namespace Prism.Features.Models.Application.ProbeCapabilities;

/// <summary>
/// Handles probing a provider instance for its actual capabilities.
/// Triggers a live probe and updates the persisted capability data.
/// </summary>
public sealed class ProbeCapabilitiesHandler
{
    private readonly IProviderCapabilityRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProbeCapabilitiesHandler"/> class.
    /// </summary>
    /// <param name="registry">The capability registry for probing providers.</param>
    public ProbeCapabilitiesHandler(IProviderCapabilityRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Probes the specified provider instance and returns the updated capability snapshot.
    /// </summary>
    /// <param name="instanceId">The provider instance to probe.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the probed capability snapshot.</returns>
    public async Task<Result<ProviderCapabilitySnapshot>> HandleAsync(Guid instanceId, CancellationToken ct)
    {
        return await _registry.ProbeAsync(instanceId, ct);
    }
}
