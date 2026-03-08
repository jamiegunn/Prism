using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.CreateExperiment;

/// <summary>
/// Handles creation of a new experiment within a project.
/// </summary>
public sealed class CreateExperimentHandler
{
    private readonly AppDbContext _db;
    private readonly IValidator<CreateExperimentCommand> _validator;
    private readonly ILogger<CreateExperimentHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateExperimentHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="validator">The command validator.</param>
    /// <param name="logger">The logger instance.</param>
    public CreateExperimentHandler(
        AppDbContext db,
        IValidator<CreateExperimentCommand> validator,
        ILogger<CreateExperimentHandler> logger)
    {
        _db = db;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new experiment and persists it to the database.
    /// </summary>
    /// <param name="command">The create experiment command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the created experiment DTO on success.</returns>
    public async Task<Result<ExperimentDto>> HandleAsync(CreateExperimentCommand command, CancellationToken ct)
    {
        ValidationResult validation = await _validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            return Error.Validation(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        bool projectExists = await _db.Set<Project>()
            .AnyAsync(p => p.Id == command.ProjectId, ct);

        if (!projectExists)
        {
            return Error.NotFound($"Project '{command.ProjectId}' was not found.");
        }

        var experiment = new Experiment
        {
            ProjectId = command.ProjectId,
            Name = command.Name,
            Description = command.Description,
            Hypothesis = command.Hypothesis
        };

        _db.Set<Experiment>().Add(experiment);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created experiment {ExperimentId} in project {ProjectId}",
            experiment.Id, command.ProjectId);

        return ExperimentDto.FromEntity(experiment, 0);
    }
}
