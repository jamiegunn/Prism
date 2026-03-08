using Microsoft.EntityFrameworkCore;
using Prism.Features.Evaluation.Application.Dtos;
using Prism.Features.Evaluation.Domain;

namespace Prism.Features.Evaluation.Application.CancelEvaluation;

/// <summary>
/// Command to cancel a running evaluation.
/// </summary>
public sealed record CancelEvaluationCommand(Guid Id);

/// <summary>
/// Handles cancelling an evaluation.
/// </summary>
public sealed class CancelEvaluationHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<CancelEvaluationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelEvaluationHandler"/> class.
    /// </summary>
    public CancelEvaluationHandler(AppDbContext db, ILogger<CancelEvaluationHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Handles the cancel evaluation command.
    /// </summary>
    public async Task<Result<EvaluationDto>> HandleAsync(CancelEvaluationCommand command, CancellationToken ct)
    {
        EvaluationEntity? evaluation = await _db.Set<EvaluationEntity>()
            .FirstOrDefaultAsync(e => e.Id == command.Id, ct);

        if (evaluation is null)
        {
            return Error.NotFound($"Evaluation {command.Id} not found.");
        }

        if (evaluation.Status is not (EvaluationStatus.Pending or EvaluationStatus.Running or EvaluationStatus.Paused))
        {
            return Error.Validation($"Cannot cancel evaluation in {evaluation.Status} status.");
        }

        evaluation.Status = EvaluationStatus.Cancelled;
        evaluation.FinishedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Cancelled evaluation {EvaluationId}", evaluation.Id);

        return EvaluationDto.FromEntity(evaluation);
    }
}
