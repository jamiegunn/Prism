using FluentValidation;

namespace Prism.Features.Experiments.Application.CreateExperiment;

/// <summary>
/// Validates a <see cref="CreateExperimentCommand"/>.
/// </summary>
public sealed class CreateExperimentValidator : AbstractValidator<CreateExperimentCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateExperimentValidator"/> class.
    /// </summary>
    public CreateExperimentValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Project ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Experiment name is required.")
            .MaximumLength(200).WithMessage("Experiment name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");
    }
}
