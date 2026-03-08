using Microsoft.EntityFrameworkCore;
using Prism.Features.Datasets.Domain;
using Prism.Features.Evaluation.Application.Dtos;
using Prism.Features.Evaluation.Domain;

namespace Prism.Features.Evaluation.Application.StartEvaluation;

/// <summary>
/// Command to start a new evaluation against a dataset.
/// </summary>
public sealed record StartEvaluationCommand(
    string Name,
    Guid DatasetId,
    string? SplitLabel,
    Guid? ProjectId,
    List<string> Models,
    Guid? PromptVersionId,
    List<string> ScoringMethods,
    Dictionary<string, object?>? Config);

/// <summary>
/// Creates an evaluation entity and enqueues it for background processing.
/// </summary>
public sealed class StartEvaluationHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<StartEvaluationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartEvaluationHandler"/> class.
    /// </summary>
    public StartEvaluationHandler(AppDbContext db, ILogger<StartEvaluationHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Handles the start evaluation command.
    /// </summary>
    public async Task<Result<EvaluationDto>> HandleAsync(StartEvaluationCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return Error.Validation("Evaluation name is required.");
        }

        if (command.Models.Count == 0)
        {
            return Error.Validation("At least one model must be specified.");
        }

        if (command.ScoringMethods.Count == 0)
        {
            return Error.Validation("At least one scoring method must be specified.");
        }

        Dataset? dataset = await _db.Set<Dataset>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == command.DatasetId, ct);

        if (dataset is null)
        {
            return Error.NotFound($"Dataset {command.DatasetId} not found.");
        }

        // Count records to evaluate
        IQueryable<DatasetRecord> recordsQuery = _db.Set<DatasetRecord>()
            .Where(r => r.DatasetId == command.DatasetId);

        if (!string.IsNullOrWhiteSpace(command.SplitLabel))
        {
            recordsQuery = recordsQuery.Where(r => r.SplitLabel == command.SplitLabel);
        }

        int recordCount = await recordsQuery.CountAsync(ct);
        if (recordCount == 0)
        {
            return Error.Validation("No records found in the dataset for the specified split.");
        }

        var evaluation = new EvaluationEntity
        {
            Name = command.Name,
            DatasetId = command.DatasetId,
            SplitLabel = command.SplitLabel,
            ProjectId = command.ProjectId,
            Models = command.Models,
            PromptVersionId = command.PromptVersionId,
            ScoringMethods = command.ScoringMethods,
            Config = command.Config ?? new Dictionary<string, object?>(),
            Status = EvaluationStatus.Pending,
            TotalRecords = recordCount * command.Models.Count
        };

        _db.Set<EvaluationEntity>().Add(evaluation);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created evaluation {EvaluationId} '{EvaluationName}' with {ModelCount} models against {RecordCount} records",
            evaluation.Id, evaluation.Name, command.Models.Count, recordCount);

        return EvaluationDto.FromEntity(evaluation);
    }
}
