using System.Text;
using Microsoft.EntityFrameworkCore;
using Prism.Common.Inference.Models;
using Prism.Features.Playground.Application.Dtos;
using Prism.Features.Playground.Domain;

namespace Prism.Features.Playground.Application.ExportConversation;

/// <summary>
/// Handles exporting a playground conversation in various formats (JSON, Markdown, JSONL).
/// </summary>
public sealed class ExportConversationHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<ExportConversationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportConversationHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public ExportConversationHandler(AppDbContext db, ILogger<ExportConversationHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Exports the conversation in the requested format.
    /// </summary>
    /// <param name="query">The query containing the conversation ID and export format.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the export data on success, or a not-found error.</returns>
    public async Task<Result<ExportResult>> HandleAsync(ExportConversationQuery query, CancellationToken ct)
    {
        Conversation? conversation = await _db.Set<Conversation>()
            .Include(c => c.Messages)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == query.Id, ct);

        if (conversation is null)
        {
            _logger.LogWarning("Export requested for non-existent conversation {ConversationId}", query.Id);
            return Error.NotFound($"Conversation '{query.Id}' was not found.");
        }

        ExportResult exportResult = query.Format switch
        {
            ExportFormat.Json => ExportAsJson(conversation),
            ExportFormat.Markdown => ExportAsMarkdown(conversation),
            ExportFormat.Jsonl => ExportAsJsonl(conversation),
            _ => ExportAsJson(conversation)
        };

        _logger.LogInformation(
            "Exported conversation {ConversationId} as {Format}",
            query.Id, query.Format);

        return exportResult;
    }

    /// <summary>
    /// Exports the conversation as a full JSON document with structured logprobs.
    /// </summary>
    private static ExportResult ExportAsJson(Conversation conversation)
    {
        ConversationDto dto = ConversationDto.FromEntity(conversation, includeLogprobs: true);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        string json = JsonSerializer.Serialize(dto, options);
        string fileName = $"conversation-{conversation.Id:N}.json";

        return new ExportResult(json, "application/json", fileName);
    }

    /// <summary>
    /// Exports the conversation as a formatted Markdown chat transcript.
    /// </summary>
    private static ExportResult ExportAsMarkdown(Conversation conversation)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {conversation.Title}");
        sb.AppendLine();
        sb.AppendLine($"**Model:** {conversation.ModelId}");
        sb.AppendLine($"**Created:** {conversation.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Total Tokens:** {conversation.TotalTokens}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(conversation.SystemPrompt))
        {
            sb.AppendLine("## System Prompt");
            sb.AppendLine();
            sb.AppendLine(conversation.SystemPrompt);
            sb.AppendLine();
        }

        sb.AppendLine("## Conversation");
        sb.AppendLine();

        foreach (Message message in conversation.Messages.OrderBy(m => m.SortOrder))
        {
            string roleLabel = message.Role switch
            {
                MessageRole.System => "System",
                MessageRole.User => "User",
                MessageRole.Assistant => "Assistant",
                _ => "Unknown"
            };

            sb.AppendLine($"### {roleLabel}");
            sb.AppendLine();
            sb.AppendLine(message.Content);
            sb.AppendLine();

            if (message.Role == MessageRole.Assistant)
            {
                var metrics = new List<string>();
                if (message.TokenCount.HasValue) metrics.Add($"Tokens: {message.TokenCount}");
                if (message.LatencyMs.HasValue) metrics.Add($"Latency: {message.LatencyMs}ms");
                if (message.TtftMs.HasValue) metrics.Add($"TTFT: {message.TtftMs}ms");
                if (message.TokensPerSecond.HasValue) metrics.Add($"Speed: {message.TokensPerSecond:F1} tok/s");
                if (message.Perplexity.HasValue) metrics.Add($"Perplexity: {message.Perplexity:F2}");

                if (metrics.Count > 0)
                {
                    sb.AppendLine($"*{string.Join(" | ", metrics)}*");
                    sb.AppendLine();
                }
            }
        }

        string content = sb.ToString();
        string fileName = $"conversation-{conversation.Id:N}.md";

        return new ExportResult(content, "text/markdown", fileName);
    }

    /// <summary>
    /// Exports the conversation as JSON Lines with one message per line.
    /// </summary>
    private static ExportResult ExportAsJsonl(Conversation conversation)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var sb = new StringBuilder();

        foreach (Message message in conversation.Messages.OrderBy(m => m.SortOrder))
        {
            LogprobsData? logprobs = null;
            if (message.LogprobsJson is not null)
            {
                logprobs = JsonSerializer.Deserialize<LogprobsData>(message.LogprobsJson);
            }

            var line = new
            {
                id = message.Id,
                conversationId = message.ConversationId,
                role = message.Role.ToString().ToLowerInvariant(),
                content = message.Content,
                tokenCount = message.TokenCount,
                logprobs,
                perplexity = message.Perplexity,
                latencyMs = message.LatencyMs,
                ttftMs = message.TtftMs,
                tokensPerSecond = message.TokensPerSecond,
                finishReason = message.FinishReason,
                createdAt = message.CreatedAt
            };

            sb.AppendLine(JsonSerializer.Serialize(line, options));
        }

        string content = sb.ToString();
        string fileName = $"conversation-{conversation.Id:N}.jsonl";

        return new ExportResult(content, "application/jsonl", fileName);
    }
}
