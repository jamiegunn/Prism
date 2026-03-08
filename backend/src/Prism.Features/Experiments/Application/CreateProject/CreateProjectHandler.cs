using FluentValidation;
using FluentValidation.Results;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Application.CreateProject;

/// <summary>
/// Handles creation of a new research project.
/// </summary>
public sealed class CreateProjectHandler
{
    private readonly AppDbContext _db;
    private readonly IValidator<CreateProjectCommand> _validator;
    private readonly ILogger<CreateProjectHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateProjectHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="validator">The command validator.</param>
    /// <param name="logger">The logger instance.</param>
    public CreateProjectHandler(
        AppDbContext db,
        IValidator<CreateProjectCommand> validator,
        ILogger<CreateProjectHandler> logger)
    {
        _db = db;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new project and persists it to the database.
    /// </summary>
    /// <param name="command">The create project command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the created project DTO on success.</returns>
    public async Task<Result<ProjectDto>> HandleAsync(CreateProjectCommand command, CancellationToken ct)
    {
        ValidationResult validation = await _validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            return Error.Validation(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var project = new Project
        {
            Name = command.Name,
            Description = command.Description
        };

        _db.Set<Project>().Add(project);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created project {ProjectId} with name {ProjectName}", project.Id, project.Name);

        return ProjectDto.FromEntity(project, 0);
    }
}
