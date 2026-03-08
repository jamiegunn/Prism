using Microsoft.EntityFrameworkCore;
using Prism.Features.Playground.Domain;

namespace Prism.Features.Playground.Application.DeleteConversation;

/// <summary>
/// Handles deletion of a playground conversation and all its messages.
/// </summary>
public sealed class DeleteConversationHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeleteConversationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteConversationHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public DeleteConversationHandler(AppDbContext db, ILogger<DeleteConversationHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Deletes the conversation with the specified ID and all its messages.
    /// </summary>
    /// <param name="command">The command containing the conversation ID to delete.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or a not-found error.</returns>
    public async Task<Result> HandleAsync(DeleteConversationCommand command, CancellationToken ct)
    {
        Conversation? conversation = await _db.Set<Conversation>()
            .FirstOrDefaultAsync(c => c.Id == command.Id, ct);

        if (conversation is null)
        {
            _logger.LogWarning("Attempted to delete non-existent conversation {ConversationId}", command.Id);
            return Error.NotFound($"Conversation '{command.Id}' was not found.");
        }

        _db.Set<Conversation>().Remove(conversation);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted conversation {ConversationId}", command.Id);

        return Result.Success();
    }
}
