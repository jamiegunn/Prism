namespace Prism.Features.Playground.Application.GetConversation;

/// <summary>
/// Query to retrieve a single playground conversation by ID.
/// </summary>
/// <param name="Id">The conversation identifier.</param>
/// <param name="IncludeLogprobs">Whether to include deserialized logprobs data in message DTOs.</param>
public sealed record GetConversationQuery(Guid Id, bool IncludeLogprobs = true);
