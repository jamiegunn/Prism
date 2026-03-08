using Microsoft.EntityFrameworkCore;
using Prism.Features.Playground.Application.Dtos;
using Prism.Features.Playground.Domain;

namespace Prism.Features.Playground.Application.GetConversation;

/// <summary>
/// Handles retrieval of a single playground conversation with all its messages.
/// </summary>
public sealed class GetConversationHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<GetConversationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetConversationHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public GetConversationHandler(AppDbContext db, ILogger<GetConversationHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a conversation by its identifier, including all messages.
    /// </summary>
    /// <param name="query">The query containing the conversation ID and options.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the conversation DTO on success, or a not-found error.</returns>
    public async Task<Result<ConversationDto>> HandleAsync(GetConversationQuery query, CancellationToken ct)
    {
        Conversation? conversation = await _db.Set<Conversation>()
            .Include(c => c.Messages)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == query.Id, ct);

        if (conversation is null)
        {
            _logger.LogWarning("Conversation {ConversationId} was not found", query.Id);
            return Error.NotFound($"Conversation '{query.Id}' was not found.");
        }

        ConversationDto dto = ConversationDto.FromEntity(conversation, query.IncludeLogprobs);
        return dto;
    }
}
