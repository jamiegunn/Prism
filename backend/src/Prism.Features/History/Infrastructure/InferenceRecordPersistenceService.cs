using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Features.History.Domain;

namespace Prism.Features.History.Infrastructure;

/// <summary>
/// Background service that consumes <see cref="InferenceRecordData"/> from a channel
/// and persists each record to the database as an <see cref="InferenceRecord"/> entity.
/// Runs for the lifetime of the application and never crashes on individual record failures.
/// </summary>
public sealed class InferenceRecordPersistenceService : BackgroundService
{
    private readonly Channel<InferenceRecordData> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InferenceRecordPersistenceService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="InferenceRecordPersistenceService"/> class.
    /// </summary>
    /// <param name="channel">The channel to read inference records from.</param>
    /// <param name="scopeFactory">The service scope factory for creating scoped database contexts.</param>
    /// <param name="logger">The logger instance.</param>
    public InferenceRecordPersistenceService(
        Channel<InferenceRecordData> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<InferenceRecordPersistenceService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Continuously reads inference records from the channel and persists them to the database.
    /// Catches and logs all errors to prevent the background service from crashing.
    /// </summary>
    /// <param name="stoppingToken">A token that signals when the service should stop.</param>
    /// <returns>A task representing the background operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InferenceRecordPersistenceService started");

        try
        {
            await foreach (InferenceRecordData data in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await PersistRecordAsync(data, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to persist inference record {RecordId}", data.Id);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("InferenceRecordPersistenceService stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "InferenceRecordPersistenceService encountered an unexpected error");
        }
    }

    /// <summary>
    /// Converts an <see cref="InferenceRecordData"/> to an <see cref="InferenceRecord"/> entity
    /// and saves it to the database within a scoped context.
    /// </summary>
    /// <param name="data">The inference record data to persist.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    private async Task PersistRecordAsync(InferenceRecordData data, CancellationToken ct)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        InferenceRecord record = MapToEntity(data);

        db.Set<InferenceRecord>().Add(record);
        await db.SaveChangesAsync(ct);

        _logger.LogDebug("Persisted inference record {RecordId} for model {Model}", data.Id, data.Request.Model);
    }

    /// <summary>
    /// Maps an <see cref="InferenceRecordData"/> to an <see cref="InferenceRecord"/> entity,
    /// serializing request, response, and environment snapshot to JSON.
    /// </summary>
    /// <param name="data">The inference record data to map.</param>
    /// <returns>A new <see cref="InferenceRecord"/> entity ready for persistence.</returns>
    private static InferenceRecord MapToEntity(InferenceRecordData data)
    {
        UsageInfo? usage = data.Response?.Usage;

        return new InferenceRecord
        {
            Id = data.Id,
            SourceModule = data.SourceModule ?? "unknown",
            ProviderName = data.ProviderName,
            ProviderType = data.ProviderType,
            ProviderEndpoint = data.Endpoint,
            Model = data.Request.Model,
            RequestJson = JsonSerializer.Serialize(data.Request, JsonOptions),
            ResponseJson = data.Response is not null
                ? JsonSerializer.Serialize(data.Response, JsonOptions)
                : null,
            PromptTokens = usage?.PromptTokens ?? 0,
            CompletionTokens = usage?.CompletionTokens ?? 0,
            TotalTokens = usage?.TotalTokens ?? 0,
            LatencyMs = data.LatencyMs,
            TtftMs = data.Response?.Timing?.TtftMs is long ttft ? (int)ttft : null,
            Perplexity = null,
            IsSuccess = data.IsSuccess,
            ErrorMessage = data.ErrorMessage,
            Tags = [],
            StartedAt = data.StartedAt,
            CompletedAt = data.CompletedAt,
            EnvironmentJson = data.Environment is not null
                ? JsonSerializer.Serialize(data.Environment, JsonOptions)
                : null
        };
    }
}
