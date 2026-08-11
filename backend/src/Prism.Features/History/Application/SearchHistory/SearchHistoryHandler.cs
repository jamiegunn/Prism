using Microsoft.EntityFrameworkCore;
using Prism.Common.Abstractions;
using Prism.Common.Inference.Models;
using Prism.Features.History.Application.Dtos;
using Prism.Features.History.Domain;

namespace Prism.Features.History.Application.SearchHistory;

/// <summary>
/// Handles paginated searching and filtering of inference history records.
/// </summary>
public sealed class SearchHistoryHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<SearchHistoryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchHistoryHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public SearchHistoryHandler(AppDbContext db, ILogger<SearchHistoryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Searches inference history records with optional filters and returns a paginated result set.
    /// Records are ordered by <c>StartedAt</c> descending (most recent first).
    /// </summary>
    /// <param name="query">The search query containing filters and pagination parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the paged inference record summaries.</returns>
    public async Task<Result<PagedResult<InferenceRecordSummaryDto>>> HandleAsync(
        SearchHistoryQuery query, CancellationToken ct)
    {
        IQueryable<InferenceRecord> queryable = _db.Set<InferenceRecord>()
            .AsNoTracking();

        queryable = HistoryFilters.Apply(queryable, query);

        int totalCount = await queryable.CountAsync(ct);

        List<InferenceRecord> records = await queryable
            .OrderByDescending(r => r.StartedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        List<InferenceRecordSummaryDto> items = records
            .Select(MapToSummaryDto)
            .ToList();

        var pagedResult = new PagedResult<InferenceRecordSummaryDto>(
            items, totalCount, query.Page, query.PageSize);

        return pagedResult;
    }

    /// <summary>
    /// Maps an <see cref="InferenceRecord"/> entity to an <see cref="InferenceRecordSummaryDto"/>,
    /// extracting prompt and response previews from the serialized JSON.
    /// </summary>
    /// <param name="record">The inference record entity to map.</param>
    /// <returns>A summary DTO for the record.</returns>
    private static InferenceRecordSummaryDto MapToSummaryDto(InferenceRecord record)
    {
        string promptPreview = ExtractPromptPreview(record.RequestJson);
        string? responsePreview = ExtractResponsePreview(record.ResponseJson);

        return new InferenceRecordSummaryDto(
            record.Id,
            record.SourceModule,
            record.Model,
            record.ProviderName,
            promptPreview,
            responsePreview,
            record.PromptTokens,
            record.CompletionTokens,
            record.LatencyMs,
            record.IsSuccess,
            record.Tags,
            record.StartedAt);
    }

    /// <summary>
    /// Extracts the first 100 characters of the first user message from a serialized ChatRequest JSON.
    /// </summary>
    /// <param name="requestJson">The serialized ChatRequest JSON string.</param>
    /// <returns>A truncated preview of the first user message, or "(no prompt)" if extraction fails.</returns>
    private static string ExtractPromptPreview(string requestJson)
    {
        try
        {
            ChatRequest? request = JsonSerializer.Deserialize<ChatRequest>(requestJson, JsonOptions);
            if (request?.Messages is { Count: > 0 })
            {
                ChatMessage? userMessage = request.Messages
                    .FirstOrDefault(m => m.Role == ChatMessage.UserRole);

                if (userMessage is not null)
                {
                    return Truncate(userMessage.Content, 100);
                }

                return Truncate(request.Messages[0].Content, 100);
            }
        }
        catch (JsonException)
        {
            // Swallow deserialization errors for preview extraction
        }

        return "(no prompt)";
    }

    /// <summary>
    /// Extracts the first 100 characters of response content from a serialized ChatResponse JSON.
    /// </summary>
    /// <param name="responseJson">The serialized ChatResponse JSON string, or null.</param>
    /// <returns>A truncated preview of the response content, or null if unavailable.</returns>
    private static string? ExtractResponsePreview(string? responseJson)
    {
        if (responseJson is null)
        {
            return null;
        }

        try
        {
            ChatResponse? response = JsonSerializer.Deserialize<ChatResponse>(responseJson, JsonOptions);
            if (response is not null && !string.IsNullOrEmpty(response.Content))
            {
                return Truncate(response.Content, 100);
            }
        }
        catch (JsonException)
        {
            // Swallow deserialization errors for preview extraction
        }

        return null;
    }

    /// <summary>
    /// Truncates a string to the specified maximum length, appending an ellipsis if truncated.
    /// </summary>
    /// <param name="value">The string to truncate.</param>
    /// <param name="maxLength">The maximum number of characters.</param>
    /// <returns>The truncated string.</returns>
    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
