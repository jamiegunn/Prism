using FluentValidation;
using Prism.Common.Inference;

namespace Prism.Features.Models.Application.RegisterInstance;

/// <summary>
/// Validates the <see cref="RegisterInstanceCommand"/> before processing.
/// </summary>
public sealed class RegisterInstanceValidator : AbstractValidator<RegisterInstanceCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterInstanceValidator"/> class
    /// and configures the validation rules.
    /// </summary>
    public RegisterInstanceValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Instance name is required.")
            .MaximumLength(200)
            .WithMessage("Instance name must not exceed 200 characters.");

        RuleFor(x => x.Endpoint)
            .NotEmpty()
            .WithMessage("Endpoint URL is required.")
            .MaximumLength(500)
            .WithMessage("Endpoint URL must not exceed 500 characters.")
            .Must(BeAValidUrl)
            .WithMessage("Endpoint must be a valid URL.");

        RuleFor(x => x.ProviderType)
            .IsInEnum()
            .WithMessage("Provider type must be a valid value.");
    }

    private static bool BeAValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
