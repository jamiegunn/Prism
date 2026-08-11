using Microsoft.EntityFrameworkCore;
using Prism.Common.Inference.Models;
using Prism.Features.Evaluation.Domain;

namespace Prism.Features.Evaluation.Application.GetCalibration;

/// <summary>
/// Query for the calibration of one model's answers in an evaluation.
/// </summary>
/// <param name="EvaluationId">The evaluation.</param>
/// <param name="Model">The model to compute calibration for; null means the first model with
/// any usable predictions.</param>
public sealed record GetCalibrationQuery(Guid EvaluationId, string? Model);

/// <summary>
/// One prediction for the reliability diagram.
/// </summary>
/// <param name="Confidence">Sequence confidence in [0, 1].</param>
/// <param name="IsCorrect">Whether exact_match judged the answer correct.</param>
public sealed record CalibrationPredictionDto(double Confidence, bool IsCorrect);

/// <summary>
/// Calibration of a model's answers: the per-prediction points a reliability diagram bins,
/// plus ECE and Brier. The counts state why predictions were excluded, so an empty diagram
/// can say which prerequisite is missing instead of rendering an unexplained blank.
/// </summary>
/// <param name="EvaluationId">The evaluation.</param>
/// <param name="Model">The model these predictions belong to.</param>
/// <param name="Predictions">The usable predictions.</param>
/// <param name="Ece">Expected Calibration Error, or null with no predictions.</param>
/// <param name="Brier">Brier score, or null with no predictions.</param>
/// <param name="BinCount">The bin count the ECE was computed with — part of its definition.</param>
/// <param name="TotalResults">All successful results for the model.</param>
/// <param name="WithLogprobs">How many of those carried logprobs.</param>
/// <param name="WithLabel">How many also carried an exact_match label — the usable count.</param>
/// <param name="Definition">The full definition of confidence, label, ECE and Brier as
/// computed here.</param>
public sealed record CalibrationDto(
    Guid EvaluationId,
    string Model,
    List<CalibrationPredictionDto> Predictions,
    double? Ece,
    double? Brier,
    int BinCount,
    int TotalResults,
    int WithLogprobs,
    int WithLabel,
    string Definition);

/// <summary>
/// Computes calibration from stored logprobs and the exact_match label.
/// </summary>
/// <remarks>
/// Confidence is the geometric mean of the <em>chosen</em> tokens' probabilities,
/// <c>exp(mean logprob)</c> — the chosen token's own probability, never the top-1
/// alternative's, which differs whenever sampling picked a non-argmax token. The correctness
/// label is <c>exact_match == 1.0</c>; items scored by other methods alone have no boolean
/// label and are excluded rather than guessed.
/// </remarks>
public sealed class GetCalibrationHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Case-insensitive so both storage conventions parse: the batch runner stores
    /// PascalCase (default serializer), API-shaped payloads are camelCase.
    /// </summary>
    private static readonly JsonSerializerOptions LogprobsJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCalibrationHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public GetCalibrationHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// The definition string returned with every calibration, kept next to the numbers.
    /// </summary>
    public static readonly string Definition =
        "Confidence = exp(mean chosen-token logprob), the geometric mean of the chosen " +
        "tokens' probabilities (chosen token, not top-1 alternative). Label: exact_match " +
        $"score == 1.0. ECE: {CalibrationMetrics.DefaultBinCount} equal-width bins, " +
        "Σ (n_b/N)·|accuracy − mean confidence|. Brier: mean (confidence − label)², in [0, 1].";

    /// <summary>
    /// Handles the calibration query.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The calibration, or NotFound for a missing evaluation or model.</returns>
    public async Task<Result<CalibrationDto>> HandleAsync(GetCalibrationQuery query, CancellationToken ct)
    {
        bool exists = await _db.Set<EvaluationEntity>()
            .AnyAsync(e => e.Id == query.EvaluationId, ct);

        if (!exists)
        {
            return Error.NotFound($"Evaluation {query.EvaluationId} not found.");
        }

        IQueryable<EvaluationResult> resultsQuery = _db.Set<EvaluationResult>()
            .AsNoTracking()
            .Where(r => r.EvaluationId == query.EvaluationId && r.Error == null);

        if (!string.IsNullOrWhiteSpace(query.Model))
        {
            resultsQuery = resultsQuery.Where(r => r.Model == query.Model);
        }

        List<EvaluationResult> results = await resultsQuery.ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(query.Model) && results.Count == 0)
        {
            bool modelExists = await _db.Set<EvaluationResult>()
                .AnyAsync(r => r.EvaluationId == query.EvaluationId && r.Model == query.Model, ct);

            if (!modelExists)
            {
                return Error.NotFound(
                    $"Model '{query.Model}' has no results in evaluation {query.EvaluationId}.");
            }
        }

        string model = query.Model
            ?? results.Select(r => r.Model).FirstOrDefault()
            ?? "";

        List<EvaluationResult> modelResults = results.Where(r => r.Model == model).ToList();

        int withLogprobs = 0;
        var predictions = new List<CalibrationMetrics.Prediction>();

        foreach (EvaluationResult result in modelResults)
        {
            double? confidence = SequenceConfidence(result.LogprobsData);

            if (confidence is null)
            {
                continue;
            }

            withLogprobs++;

            if (!result.Scores.TryGetValue("exact_match", out double exactMatch))
            {
                continue;
            }

            predictions.Add(new CalibrationMetrics.Prediction(confidence.Value, exactMatch >= 1.0));
        }

        return new CalibrationDto(
            query.EvaluationId,
            model,
            predictions.Select(p => new CalibrationPredictionDto(p.Confidence, p.IsCorrect)).ToList(),
            CalibrationMetrics.ExpectedCalibrationError(predictions),
            CalibrationMetrics.BrierScore(predictions),
            CalibrationMetrics.DefaultBinCount,
            modelResults.Count,
            withLogprobs,
            predictions.Count,
            Definition);
    }

    /// <summary>
    /// Extracts sequence confidence from stored logprobs JSON: <c>exp(mean logprob)</c> over
    /// the chosen tokens. Returns null when there are no logprobs — absence is not zero
    /// confidence.
    /// </summary>
    /// <param name="logprobsJson">The stored logprobs JSON, or null.</param>
    /// <returns>The confidence in [0, 1], or null.</returns>
    internal static double? SequenceConfidence(string? logprobsJson)
    {
        if (string.IsNullOrWhiteSpace(logprobsJson))
        {
            return null;
        }

        LogprobsData? logprobs;
        try
        {
            logprobs = JsonSerializer.Deserialize<LogprobsData>(logprobsJson, LogprobsJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (logprobs is not { Tokens.Count: > 0 })
        {
            return null;
        }

        double meanLogprob = logprobs.Tokens.Average(t => t.Logprob);

        return Math.Clamp(Math.Exp(meanLogprob), 0.0, 1.0);
    }
}
