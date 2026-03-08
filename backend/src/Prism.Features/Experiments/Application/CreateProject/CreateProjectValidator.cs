using FluentValidation;

namespace Prism.Features.Experiments.Application.CreateProject;

/// <summary>
/// Validates a <see cref="CreateProjectCommand"/>.
/// </summary>
public sealed class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateProjectValidator"/> class.
    /// </summary>
    public CreateProjectValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(200).WithMessage("Project name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");
    }
}
