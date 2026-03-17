using Prism.Common.Results;

namespace Prism.Common.Inference.Runtime;

/// <summary>
/// Resolves an <see cref="IInferenceProvider"/> from a provider instance ID.
/// Bridges the runtime to instance storage and the provider factory.
/// </summary>
public interface IRuntimeProviderResolver
{
    /// <summary>
    /// Resolves an inference provider for the given registered instance.
    /// Loads instance metadata from storage and creates a configured provider.
    /// </summary>
    /// <param name="instanceId">The unique identifier of the registered provider instance.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the resolved provider, or an error if the instance is not found.</returns>
    Task<Result<IInferenceProvider>> ResolveAsync(Guid instanceId, CancellationToken ct);
}
