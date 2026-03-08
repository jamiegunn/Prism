using Prism.Common.Inference.Models;
using Prism.Common.Results;

namespace Prism.Common.Inference;

/// <summary>
/// Extended interface for inference providers that support hot-reloading models
/// without restarting the provider server.
/// </summary>
public interface IHotReloadableProvider : IInferenceProvider
{
    /// <summary>
    /// Lists all models available on the provider that can be loaded.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the list of available models.</returns>
    Task<Result<IReadOnlyList<AvailableModel>>> ListAvailableModelsAsync(CancellationToken ct);

    /// <summary>
    /// Loads a model by its identifier, making it available for inference.
    /// </summary>
    /// <param name="modelId">The identifier of the model to load.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure of the load operation.</returns>
    Task<Result> LoadModelAsync(string modelId, CancellationToken ct);

    /// <summary>
    /// Unloads a model by its identifier, freeing resources.
    /// </summary>
    /// <param name="modelId">The identifier of the model to unload.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure of the unload operation.</returns>
    Task<Result> UnloadModelAsync(string modelId, CancellationToken ct);
}
