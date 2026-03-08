using Prism.Common.Results;

namespace Prism.Common.Inference;

/// <summary>
/// Factory for resolving and managing inference provider instances.
/// Supports resolution by instance ID or endpoint, and handles provider lifecycle.
/// </summary>
public interface IInferenceProviderFactory
{
    /// <summary>
    /// Gets a registered provider instance by its unique identifier.
    /// </summary>
    /// <param name="instanceId">The unique identifier of the provider instance.</param>
    /// <returns>A result containing the provider on success, or NotFound if not registered.</returns>
    Result<IInferenceProvider> GetProvider(Guid instanceId);

    /// <summary>
    /// Gets a registered provider instance by its endpoint and type.
    /// </summary>
    /// <param name="endpoint">The endpoint URL of the provider.</param>
    /// <param name="type">The type of inference provider.</param>
    /// <returns>A result containing the provider on success, or NotFound if not registered.</returns>
    Result<IInferenceProvider> GetProvider(string endpoint, InferenceProviderType type);

    /// <summary>
    /// Gets all registered provider instances.
    /// </summary>
    /// <returns>A read-only list of all registered providers.</returns>
    IReadOnlyList<IInferenceProvider> GetAllProviders();

    /// <summary>
    /// Registers a new provider instance with the factory.
    /// </summary>
    /// <param name="instanceId">The unique identifier for the new provider instance.</param>
    /// <param name="provider">The provider instance to register.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> RegisterProviderAsync(Guid instanceId, IInferenceProvider provider, CancellationToken ct);

    /// <summary>
    /// Unregisters a provider instance from the factory.
    /// </summary>
    /// <param name="instanceId">The unique identifier of the provider to remove.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> UnregisterProviderAsync(Guid instanceId, CancellationToken ct);

    /// <summary>
    /// Updates an existing provider instance's configuration.
    /// </summary>
    /// <param name="instanceId">The unique identifier of the provider to update.</param>
    /// <param name="provider">The new provider instance to replace the existing one.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> UpdateProviderAsync(Guid instanceId, IInferenceProvider provider, CancellationToken ct);

    /// <summary>
    /// Reloads all provider instances from the configuration file (providers.json).
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> ReloadFromConfigAsync(CancellationToken ct);
}
