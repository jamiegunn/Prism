using Prism.Features.Evaluation.Domain;
using Prism.Features.Evaluation.Domain.Scorers;

namespace Prism.Tests.Unit.Evaluation;

public sealed class ScorerTests
{
    private readonly CancellationToken _ct = CancellationToken.None;

    // ── ExactMatchScorer ───────────────────────────────────────────

    [Fact]
    public async Task ExactMatch_IdenticalStrings_ReturnsOne()
    {
        var scorer = new ExactMatchScorer();

        double score = await scorer.ScoreAsync("input", "hello world", "hello world", _ct);

        score.Should().Be(1.0);
    }

    [Fact]
    public async Task ExactMatch_DifferentStrings_ReturnsZero()
    {
        var scorer = new ExactMatchScorer();

        double score = await scorer.ScoreAsync("input", "hello", "goodbye", _ct);

        score.Should().Be(0.0);
    }

    [Fact]
    public async Task ExactMatch_CaseInsensitive_ReturnsOne()
    {
        var scorer = new ExactMatchScorer();

        double score = await scorer.ScoreAsync("input", "HELLO", "hello", _ct);

        score.Should().Be(1.0);
    }

    // ── ContainsScorer ─────────────────────────────────────────────

    [Fact]
    public async Task ContainsScorer_ContainedText_ReturnsOne()
    {
        var scorer = new ContainsScorer();

        double score = await scorer.ScoreAsync("input", "world", "hello world today", _ct);

        score.Should().Be(1.0);
    }

    [Fact]
    public async Task ContainsScorer_MissingText_ReturnsZero()
    {
        var scorer = new ContainsScorer();

        double score = await scorer.ScoreAsync("input", "missing", "hello world today", _ct);

        score.Should().Be(0.0);
    }

    // ── LengthRatioScorer ──────────────────────────────────────────

    [Fact]
    public async Task LengthRatioScorer_SameLength_ReturnsOne()
    {
        var scorer = new LengthRatioScorer();

        double score = await scorer.ScoreAsync("input", "abcd", "efgh", _ct);

        score.Should().Be(1.0);
    }

    [Fact]
    public async Task LengthRatioScorer_DoubleLengthExpected_ReturnsRatio()
    {
        var scorer = new LengthRatioScorer();

        // expected = "ab" (len 2), actual = "abcd" (len 4) => min/max = 2/4 = 0.5
        double score = await scorer.ScoreAsync("input", "ab", "abcd", _ct);

        score.Should().Be(0.5);
    }
}
