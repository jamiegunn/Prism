using FluentValidation;

namespace Prism.Features.Playground.Application.StreamChat;

/// <summary>
/// Validates the <see cref="StreamChatCommand"/> before processing.
/// </summary>
public sealed class StreamChatValidator : AbstractValidator<StreamChatCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StreamChatValidator"/> class.
    /// </summary>
    public StreamChatValidator()
    {
        RuleFor(x => x.UserMessage)
            .NotEmpty()
            .WithMessage("User message must not be empty.");

        RuleFor(x => x.InstanceId)
            .NotEmpty()
            .WithMessage("Instance ID must be specified.");
    }
}
