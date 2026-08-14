namespace Prism.Features.Rag.Application.Dtos;

/// <summary>
/// What a search returned, and whether it ran the way it was asked to.
/// </summary>
/// <param name="Results">The ranked chunks.</param>
/// <param name="DegradedReason">
/// <see langword="null"/> when the search ran as asked. Otherwise, why it could not, in terms
/// the caller can act on — the results are still real, but they did not come from the method
/// that was requested.
/// </param>
/// <remarks>
/// <para>
/// A bare list cannot say "these are half of a hybrid search". Hybrid used to fail outright when
/// embedding was unavailable, throwing away the BM25 half it had already computed; returning
/// that half silently would have been worse, because a result labelled hybrid that was really
/// keyword-only is a claim about method, and method is what a retrieval comparison is measuring.
/// </para>
/// <para>
/// So the degradation travels with the results, and callers that care about method — the
/// retrieval evaluation, above all — can refuse to score them.
/// </para>
/// </remarks>
public sealed record ChunkSearchOutcomeDto(
    List<ChunkSearchResultDto> Results,
    string? DegradedReason)
{
    /// <summary>
    /// Gets a value indicating whether the search ran the method that was asked for.
    /// </summary>
    public bool RanAsRequested => DegradedReason is null;

    /// <summary>
    /// Wraps results from a search that ran as requested.
    /// </summary>
    /// <param name="results">The ranked chunks.</param>
    /// <returns>An outcome with no degradation.</returns>
    public static ChunkSearchOutcomeDto Complete(List<ChunkSearchResultDto> results)
        => new(results, null);
}
