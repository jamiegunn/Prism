using Microsoft.EntityFrameworkCore;
using Prism.Features.History.Domain;

namespace Prism.Features.History.Application.GetTrace;

/// <summary>
/// Query for the per-token trace of one inference record.
/// </summary>
/// <param name="RecordId">The inference record.</param>
public sealed record GetTraceQuery(Guid RecordId);

/// <summary>
/// One alternative the model considered at a position.
/// </summary>
/// <param name="Token">The alternative token.</param>
/// <param name="Logprob">Its log probability.</param>
/// <param name="Probability">Its probability.</param>
public sealed record TraceAlternativeDto(string Token, double Logprob, double Probability);

/// <summary>
/// One token of the trace, in generation order.
/// </summary>
/// <param name="Position">Zero-based position in the completion.</param>
/// <param name="Token">The token text.</param>
/// <param name="Logprob">The chosen token's log probability.</param>
/// <param name="Probability">The chosen token's probability.</param>
/// <param name="Entropy">The distribution entropy at this position, in bits.</param>
/// <param name="IsSurprise">Whether the chosen token fell under the surprise threshold.</param>
/// <param name="TopLogprobs">The alternatives recorded at this position, most probable
/// first. Named to match the shape the logprobs components already consume.</param>
public sealed record TraceTokenDto(
    int Position,
    string Token,
    double Logprob,
    double Probability,
    double Entropy,
    bool IsSurprise,
    List<TraceAlternativeDto> TopLogprobs);

/// <summary>
/// A full recorded trace.
/// </summary>
/// <param name="InferenceRecordId">The record the trace belongs to.</param>
/// <param name="Perplexity">Perplexity over the completion, or null when not computed —
/// absence stays absent.</param>
/// <param name="MeanEntropy">Mean per-token entropy in bits, or null when not computed.</param>
/// <param name="AverageLogprob">Mean chosen-token log probability, or null when not computed.</param>
/// <param name="SurpriseTokenCount">How many tokens fell under the surprise threshold.</param>
/// <param name="SurpriseThreshold">The probability threshold below which a token counts as a
/// surprise — part of the definition of the surprise count.</param>
/// <param name="SchemaVersion">The trace schema version recorded at write time.</param>
/// <param name="Tokens">The per-token events in generation order.</param>
public sealed record InferenceTraceDto(
    Guid InferenceRecordId,
    double? Perplexity,
    double? MeanEntropy,
    double? AverageLogprob,
    int SurpriseTokenCount,
    double SurpriseThreshold,
    string SchemaVersion,
    List<TraceTokenDto> Tokens);

/// <summary>
/// The trace endpoint's response: either the trace, or the stated reason there is none —
/// an absent trace is a fact worth explaining, not an empty panel.
/// </summary>
/// <param name="HasTrace">Whether a trace was recorded for this call.</param>
/// <param name="AbsenceReason">Why there is no trace, when there is none.</param>
/// <param name="Trace">The trace, when recorded.</param>
public sealed record TraceResponseDto(
    bool HasTrace,
    string? AbsenceReason,
    InferenceTraceDto? Trace);

/// <summary>
/// Reads the per-token trace History has always recorded and never displayed: token-by-token
/// logprobs, entropies and surprise flags with the alternatives considered at each position.
/// </summary>
public sealed class GetTraceHandler
{
    private static readonly JsonSerializerOptions AlternativesJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTraceHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public GetTraceHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the trace query.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The trace or its stated absence; NotFound for a missing record.</returns>
    public async Task<Result<TraceResponseDto>> HandleAsync(GetTraceQuery query, CancellationToken ct)
    {
        InferenceRecord? record = await _db.Set<InferenceRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == query.RecordId, ct);

        if (record is null)
        {
            return Error.NotFound($"History record {query.RecordId} not found.");
        }

        InferenceTrace? trace = await _db.Set<InferenceTrace>()
            .AsNoTracking()
            .Include(t => t.TokenEvents)
            .FirstOrDefaultAsync(t => t.InferenceRecordId == query.RecordId, ct);

        if (trace is null)
        {
            string reason = !record.IsSuccess
                ? "The call failed before a response existed, so there are no tokens to trace."
                : "No logprobs were recorded for this call — the provider did not return "
                  + "them, or the request did not ask for them.";

            return new TraceResponseDto(false, reason, null);
        }

        List<TraceTokenDto> tokens = trace.TokenEvents
            .OrderBy(e => e.Position)
            .Select(e => new TraceTokenDto(
                e.Position,
                e.Token,
                e.Logprob,
                e.Probability,
                e.Entropy,
                e.IsSurprise,
                ParseAlternatives(e.TopAlternativesJson)))
            .ToList();

        return new TraceResponseDto(
            true,
            null,
            new InferenceTraceDto(
                trace.InferenceRecordId,
                trace.Perplexity,
                trace.MeanEntropy,
                trace.AverageLogprob,
                trace.SurpriseTokenCount,
                trace.SurpriseThreshold,
                trace.SchemaVersion,
                tokens));
    }

    /// <summary>
    /// Parses the stored alternatives JSON. Malformed or missing JSON yields an empty list —
    /// a token without alternatives is normal (the request may not have asked for any).
    /// </summary>
    /// <param name="json">The stored alternatives JSON, or null.</param>
    /// <returns>The alternatives, most probable first.</returns>
    internal static List<TraceAlternativeDto> ParseAlternatives(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<TraceAlternativeDto>>(json, AlternativesJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
