using FluentValidation;

namespace Prism.Features.History.Application.ReplaySingle;

/// <summary>
/// Validates a <see cref="ReplaySingleCommand"/> before any provider is contacted.
/// </summary>
/// <remarks>
/// The overrides are sampling parameters with defined ranges, and a value outside the range is a
/// mistake in the request rather than a fault in the server. Without these rules a temperature of
/// 99 or a negative token budget travelled to the provider, which either silently ignored it — a
/// replay that quietly did not do what was asked — or failed, which the API reported as a 503 as
/// though the inference server were at fault.
/// </remarks>
public sealed class ReplaySingleValidator : AbstractValidator<ReplaySingleCommand>
{
    /// <summary>The largest token budget a replay may request.</summary>
    private const int MaxTokenBudget = 128_000;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaySingleValidator"/> class.
    /// </summary>
    public ReplaySingleValidator()
    {
        RuleFor(x => x.RecordId)
            .NotEmpty().WithMessage("Record ID is required.");

        RuleFor(x => x.InstanceId)
            .NotEmpty().WithMessage("Target instance ID is required.");

        RuleFor(x => x.OverrideTemperature!.Value)
            .InclusiveBetween(0.0, 2.0)
            .WithMessage("Temperature override must be between 0 and 2.")
            .When(x => x.OverrideTemperature.HasValue);

        RuleFor(x => x.OverrideTopP!.Value)
            .InclusiveBetween(0.0, 1.0)
            .WithMessage("Top-P override must be between 0 and 1.")
            .When(x => x.OverrideTopP.HasValue);

        RuleFor(x => x.OverrideMaxTokens!.Value)
            .InclusiveBetween(1, MaxTokenBudget)
            .WithMessage($"Max tokens override must be between 1 and {MaxTokenBudget}.")
            .When(x => x.OverrideMaxTokens.HasValue);

        RuleFor(x => x.OverrideModel!)
            .NotEmpty().WithMessage("Model override must not be blank.")
            .MaximumLength(200).WithMessage("Model override must not exceed 200 characters.")
            .When(x => x.OverrideModel is not null);
    }
}
