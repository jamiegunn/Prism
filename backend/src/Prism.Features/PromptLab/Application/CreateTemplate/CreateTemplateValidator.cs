using FluentValidation;

namespace Prism.Features.PromptLab.Application.CreateTemplate;

/// <summary>
/// Validates a <see cref="CreateTemplateCommand"/>.
/// </summary>
public sealed class CreateTemplateValidator : AbstractValidator<CreateTemplateCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTemplateValidator"/> class.
    /// </summary>
    public CreateTemplateValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Template name is required.")
            .MaximumLength(200).WithMessage("Template name must not exceed 200 characters.");

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage("Category must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.UserTemplate)
            .NotEmpty().WithMessage("User template is required.");
    }
}
