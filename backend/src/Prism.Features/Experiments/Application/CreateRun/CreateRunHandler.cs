using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.CreateRun;

/// <summary>
/// Handles creation of a new run in an experiment.
/// </summary>
public sealed class CreateRunHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreateRunHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateRunHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public CreateRunHandler(AppDbContext db, ILogger<CreateRunHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new run and persists it to the database.
    /// </summary>
    /// <param name="command">The create run command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the created run DTO on success.</returns>
    public async Task<Result<RunDto>> HandleAsync(CreateRunCommand command, CancellationToken ct)
    {
        bool experimentExists = await _db.Set<Experiment>()
            .AnyAsync(e => e.Id == command.ExperimentId, ct);

        if (!experimentExists)
        {
            return Error.NotFound($"Experiment '{command.ExperimentId}' was not found.");
        }

        var run = new Run
        {
            ExperimentId = command.ExperimentId,
            Name = command.Name,
            Model = command.Model,
            InstanceId = command.InstanceId,
            Parameters = command.Parameters ?? new RunParameters(),
            Input = command.Input,
            Output = command.Output,
            SystemPrompt = command.SystemPrompt,
            Metrics = command.Metrics ?? new Dictionary<string, double>(),
            PromptTokens = command.PromptTokens,
            CompletionTokens = command.CompletionTokens,
            TotalTokens = command.TotalTokens,
            Cost = command.Cost,
            LatencyMs = command.LatencyMs,
            TtftMs = command.TtftMs,
            TokensPerSecond = command.TokensPerSecond,
            Perplexity = command.Perplexity,
            LogprobsData = command.LogprobsData,
            Status = command.Status,
            Error = command.Error,
            Tags = command.Tags ?? [],
            FinishReason = command.FinishReason
        };

        _db.Set<Run>().Add(run);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created run {RunId} in experiment {ExperimentId}", run.Id, command.ExperimentId);

        return RunDto.FromEntity(run);
    }
}
