using Microsoft.EntityFrameworkCore;
using Prism.Features.History.Application.Dtos;
using Prism.Features.History.Domain;

namespace Prism.Features.History.Application.GetRecord;

/// <summary>
/// Handles retrieval of a single inference record with full detail including request/response JSON.
/// </summary>
public sealed class GetRecordHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<GetRecordHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetRecordHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public GetRecordHandler(AppDbContext db, ILogger<GetRecordHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a single inference record by its identifier and returns it as a detail DTO.
    /// </summary>
    /// <param name="query">The query containing the record identifier.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the detailed inference record DTO, or a not-found error.</returns>
    public async Task<Result<InferenceRecordDetailDto>> HandleAsync(GetRecordQuery query, CancellationToken ct)
    {
        InferenceRecord? record = await _db.Set<InferenceRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == query.Id, ct);

        if (record is null)
        {
            _logger.LogWarning("Inference record {RecordId} was not found", query.Id);
            return Error.NotFound($"Inference record '{query.Id}' was not found.");
        }

        InferenceRecordDetailDto dto = MapToDetailDto(record);
        return dto;
    }

    /// <summary>
    /// Maps an <see cref="InferenceRecord"/> entity to an <see cref="InferenceRecordDetailDto"/>.
    /// </summary>
    /// <param name="record">The inference record entity.</param>
    /// <returns>A detail DTO representing the record.</returns>
    internal static InferenceRecordDetailDto MapToDetailDto(InferenceRecord record)
    {
        return new InferenceRecordDetailDto(
            record.Id,
            record.SourceModule,
            record.Model,
            record.ProviderName,
            record.ProviderEndpoint,
            record.ProviderType.ToString(),
            record.RequestJson,
            record.ResponseJson,
            record.PromptTokens,
            record.CompletionTokens,
            record.TotalTokens,
            record.LatencyMs,
            record.TtftMs,
            record.Perplexity,
            record.IsSuccess,
            record.ErrorMessage,
            record.Tags,
            record.StartedAt,
            record.CompletedAt,
            record.EnvironmentJson);
    }
}
