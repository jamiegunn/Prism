using Microsoft.EntityFrameworkCore;
using Prism.Features.Evaluation.Application.Dtos;
using Prism.Features.Evaluation.Domain;

namespace Prism.Features.Evaluation.Application.GetEvaluation;

/// <summary>
/// Query to get a single evaluation by ID.
/// </summary>
public sealed record GetEvaluationQuery(Guid Id);

/// <summary>
/// Handles getting a single evaluation.
/// </summary>
public sealed class GetEvaluationHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetEvaluationHandler"/> class.
    /// </summary>
    public GetEvaluationHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the get evaluation query.
    /// </summary>
    public async Task<Result<EvaluationDto>> HandleAsync(GetEvaluationQuery query, CancellationToken ct)
    {
        EvaluationEntity? evaluation = await _db.Set<EvaluationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == query.Id, ct);

        if (evaluation is null)
        {
            return Error.NotFound($"Evaluation {query.Id} not found.");
        }

        return EvaluationDto.FromEntity(evaluation);
    }
}
