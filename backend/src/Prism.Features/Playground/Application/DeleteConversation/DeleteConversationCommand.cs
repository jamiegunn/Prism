namespace Prism.Features.Playground.Application.DeleteConversation;

/// <summary>
/// Command to delete a playground conversation and all its messages.
/// </summary>
/// <param name="Id">The conversation identifier to delete.</param>
public sealed record DeleteConversationCommand(Guid Id);
