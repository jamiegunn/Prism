using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Results;
using Prism.Features.History.Application.GetTrace;
using Prism.Features.History.Domain;

namespace Prism.Tests.Integration;

/// <summary>
/// Proofs for the trace endpoint — the reader of the per-token data History has recorded
/// since the recording spine landed and no screen ever displayed.
/// </summary>
[Collection("Database")]
public sealed class HistoryTraceTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryTraceTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public HistoryTraceTests(DatabaseFixture fixture) => _fixture = fixture;

    private static InferenceRecord Record(bool success = true) => new()
    {
        SourceModule = $"trace-test-{Guid.NewGuid():N}",
        ProviderName = "p",
        ProviderType = InferenceProviderType.Vllm,
        ProviderEndpoint = "http://localhost:8000",
        Model = "m",
        RequestJson = "{}",
        ResponseJson = success ? "{}" : null,
        IsSuccess = success,
        ErrorMessage = success ? null : "boom",
        Tags = [],
        StartedAt = DateTime.UtcNow,
        CompletedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// The trace round-trips: events come back in position order regardless of insertion
    /// order, alternatives parse from their stored JSON, and the summary statistics ride
    /// along with the threshold that defines the surprise count.
    /// </summary>
    [Fact]
    public async Task Trace_RoundTrips_In_Position_Order_With_Alternatives()
    {
        await using AppDbContext db = _fixture.CreateContext();
        InferenceRecord record = Record();
        db.Add(record);

        var trace = new InferenceTrace
        {
            InferenceRecordId = record.Id,
            TokenEventCount = 3,
            Perplexity = 1.5,
            MeanEntropy = 0.75,
            AverageLogprob = -0.405,
            SurpriseTokenCount = 1,
            SurpriseThreshold = 0.1,
            SchemaVersion = "1.0.0",
            TokenEvents =
            [
                // Deliberately inserted out of order: the reader must sort by position.
                new TokenEvent
                {
                    InferenceTraceId = Guid.Empty, // set by EF via the navigation
                    Position = 2,
                    Token = "!",
                    Logprob = -2.5,
                    Probability = 0.082,
                    Entropy = 1.9,
                    IsSurprise = true,
                    TopAlternativesJson = """[{"token":"?","logprob":-0.9,"probability":0.406}]""",
                },
                new TokenEvent
                {
                    InferenceTraceId = Guid.Empty,
                    Position = 0,
                    Token = "Hi",
                    Logprob = -0.1,
                    Probability = 0.905,
                    Entropy = 0.2,
                    IsSurprise = false,
                    TopAlternativesJson = null, // no alternatives requested — normal
                },
                new TokenEvent
                {
                    InferenceTraceId = Guid.Empty,
                    Position = 1,
                    Token = " there",
                    Logprob = -0.3,
                    Probability = 0.741,
                    Entropy = 0.6,
                    IsSurprise = false,
                    // The column is jsonb, so Postgres rejects non-JSON outright; the
                    // realistic corruption is valid JSON of the wrong shape. Must not 500.
                    TopAlternativesJson = """{"unexpected": "shape"}""",
                },
            ],
        };
        db.Add(trace);
        await db.SaveChangesAsync();

        var handler = new GetTraceHandler(_fixture.CreateContext());
        Result<TraceResponseDto> result = await handler.HandleAsync(
            new GetTraceQuery(record.Id), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : "");
        Assert.True(result.Value.HasTrace);
        Assert.Null(result.Value.AbsenceReason);

        InferenceTraceDto dto = result.Value.Trace!;
        Assert.Equal(record.Id, dto.InferenceRecordId);
        Assert.Equal(1.5, dto.Perplexity);
        Assert.Equal(0.1, dto.SurpriseThreshold);

        Assert.Equal(["Hi", " there", "!"], dto.Tokens.Select(t => t.Token).ToArray());
        Assert.Equal([0, 1, 2], dto.Tokens.Select(t => t.Position).ToArray());

        // Alternatives: parsed where stored, empty where absent or malformed.
        Assert.Empty(dto.Tokens[0].TopLogprobs);
        Assert.Empty(dto.Tokens[1].TopLogprobs);
        TraceAlternativeDto alt = Assert.Single(dto.Tokens[2].TopLogprobs);
        Assert.Equal("?", alt.Token);
        Assert.Equal(-0.9, alt.Logprob);

        Assert.True(dto.Tokens[2].IsSurprise);
        Assert.False(dto.Tokens[0].IsSurprise);
    }

    /// <summary>
    /// A record without a trace states why — distinguishing "the call failed" from "no
    /// logprobs were recorded" — and a missing record is a 404-class NotFound, not an empty
    /// 200.
    /// </summary>
    [Fact]
    public async Task Absent_Traces_State_Why_And_Missing_Records_Are_NotFound()
    {
        await using AppDbContext db = _fixture.CreateContext();
        InferenceRecord noLogprobs = Record(success: true);
        InferenceRecord failed = Record(success: false);
        db.AddRange(noLogprobs, failed);
        await db.SaveChangesAsync();

        var handler = new GetTraceHandler(_fixture.CreateContext());

        Result<TraceResponseDto> ok = await handler.HandleAsync(
            new GetTraceQuery(noLogprobs.Id), CancellationToken.None);
        Assert.True(ok.IsSuccess);
        Assert.False(ok.Value.HasTrace);
        Assert.Contains("logprobs", ok.Value.AbsenceReason, StringComparison.OrdinalIgnoreCase);

        Result<TraceResponseDto> fail = await handler.HandleAsync(
            new GetTraceQuery(failed.Id), CancellationToken.None);
        Assert.True(fail.IsSuccess);
        Assert.False(fail.Value.HasTrace);
        Assert.Contains("failed", fail.Value.AbsenceReason, StringComparison.OrdinalIgnoreCase);

        Result<TraceResponseDto> missing = await handler.HandleAsync(
            new GetTraceQuery(Guid.NewGuid()), CancellationToken.None);
        Assert.True(missing.IsFailure);
        Assert.Equal(ErrorType.NotFound, missing.Error.Type);
    }
}
