namespace Prism.Features.Evaluation.Domain.Scorers;

/// <summary>
/// Sentence-level BLEU with sacrebleu's exact definition — 13a tokenization, case-sensitive,
/// exp smoothing, effective order, brevity penalty — scaled to 0–1. See
/// <see cref="SacreBleuMetric"/> for the ported algorithm and its differential proof.
/// </summary>
/// <remarks>
/// The per-item score this produces is a <em>sentence</em> BLEU. The corpus BLEU shown on the
/// evaluation summary is computed separately from summed n-gram statistics
/// (<see cref="SacreBleuMetric.BleuStatistics.Add"/>); the mean of these sentence scores is
/// not corpus BLEU and is never presented as such.
/// </remarks>
public sealed class BleuScorer : IScoringMethod
{
    /// <inheritdoc />
    public string Name => "bleu";

    /// <inheritdoc />
    public string Definition =>
        "Sentence BLEU-4: tokenizer 13a, case-sensitive, exp smoothing (NIST/mteval), " +
        "effective order, single reference, brevity penalty ≤ 1. Definition ported from " +
        $"sacrebleu {SacreBleuMetric.ReferenceVersion}, differential-tested to 1e-9. " +
        "Scale 0–1 (sacrebleu score / 100). Not comparable to a corpus BLEU.";

    /// <inheritdoc />
    public Task<double> ScoreAsync(string input, string expected, string actual, CancellationToken ct)
    {
        return Task.FromResult(SacreBleuMetric.SentenceScore(actual, expected) / 100.0);
    }
}
