namespace Prism.Common.Inference;

/// <summary>
/// Implemented by providers that can say what one of their models is for.
/// </summary>
/// <remarks>
/// Separate from <see cref="IInferenceProvider"/> because most backends cannot answer it. Ollama
/// can: <c>/api/show</c> returns a capability list, <c>["embedding"]</c> against
/// <c>["completion","tools"]</c>. Knowing the difference is what keeps an instance from being set
/// to a model that will only fail later, at the moment someone tries to hold a conversation with
/// an embedding model.
/// </remarks>
public interface IModelPurposeProbe : IInferenceProvider
{
    /// <summary>
    /// Reports whether a model can generate text.
    /// </summary>
    /// <param name="modelId">The model to ask about.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> or <see langword="false"/> when the server says, and
    /// <see langword="null"/> when it does not — an older server, or one that could not be
    /// reached. Callers must treat null as "unknown" and leave things as they are, never as a no.
    /// </returns>
    Task<bool?> CanGenerateTextAsync(string modelId, CancellationToken ct);
}
