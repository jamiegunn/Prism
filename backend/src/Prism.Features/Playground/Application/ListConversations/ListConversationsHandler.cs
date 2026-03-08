using Microsoft.EntityFrameworkCore;
using Prism.Common.Abstractions;
using Prism.Features.Playground.Application.Dtos;
using Prism.Features.Playground.Domain;

namespace Prism.Features.Playground.Application.ListConversations;

/// <summary>
/// Handles paginated listing of playground conversations with optional search filtering.
/// </summary>
public sealed class ListConversationsHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<ListConversationsHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListConversationsHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public ListConversationsHandler(AppDbContext db, ILogger<ListConversationsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Returns a paginated list of conversation summaries, ordered by pinned status then last message time.
    /// </summary>
    /// <param name="query">The query containing search and pagination parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the paged conversation summaries.</returns>
    public async Task<Result<PagedResult<ConversationSummaryDto>>> HandleAsync(
        ListConversationsQuery query, CancellationToken ct)
    {
        IQueryable<Conversation> queryable = _db.Set<Conversation>()
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.ToLowerInvariant();
            queryable = queryable.Where(c => c.Title.ToLower().Contains(search));
        }

        int totalCount = await queryable.CountAsync(ct);

        List<Conversation> conversations = await queryable
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.LastMessageAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        // Get message counts for each conversation in one query
        List<Guid> conversationIds = conversations.Select(c => c.Id).ToList();

        Dictionary<Guid, int> messageCounts = await _db.Set<Message>()
            .Where(m => conversationIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConversationId, x => x.Count, ct);

        List<ConversationSummaryDto> items = conversations
            .Select(c => ConversationSummaryDto.FromEntity(
                c,
                messageCounts.GetValueOrDefault(c.Id, 0)))
            .ToList();

        var pagedResult = new PagedResult<ConversationSummaryDto>(
            items, totalCount, query.Page, query.PageSize);

        return pagedResult;
    }
}
