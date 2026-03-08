using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Prism.Common.Results;
using StackExchange.Redis;

namespace Prism.Common.Jobs;

/// <summary>
/// Redis implementation of <see cref="IJobQueue"/> using Redis lists and pub/sub.
/// Provides persistent job queuing that survives application restarts.
/// </summary>
public sealed class RedisJobQueue : IJobQueue
{
    private const string QueueKeyPrefix = "prism:jobqueue:";
    private const string StatusKeyPrefix = "prism:jobstatus:";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisJobQueue> _logger;
    private readonly ConcurrentDictionary<Type, SemaphoreSlim> _waitSignals = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisJobQueue"/> class.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="logger">The logger instance.</param>
    public RedisJobQueue(IConnectionMultiplexer redis, ILogger<RedisJobQueue> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> EnqueueAsync<T>(T item, CancellationToken ct) where T : class
    {
        Guid jobId = Guid.NewGuid();
        IDatabase db = _redis.GetDatabase();

        string queueKey = GetQueueKey<T>();
        string statusKey = GetStatusKey(jobId);

        var envelope = new JobEnvelope<T>(jobId, item);
        string json = JsonSerializer.Serialize(envelope, _jsonOptions);

        await db.ListRightPushAsync(queueKey, json);
        await db.StringSetAsync(statusKey, JobStatus.Queued.ToString(), TimeSpan.FromDays(7));

        // Signal any waiting dequeue operations
        SemaphoreSlim signal = GetOrCreateSignal<T>();
        signal.Release();

        _logger.LogInformation("Enqueued job {JobId} of type {JobType} to Redis", jobId, typeof(T).Name);
        return jobId;
    }

    /// <inheritdoc />
    public async Task<Result<T>> DequeueAsync<T>(CancellationToken ct) where T : class
    {
        IDatabase db = _redis.GetDatabase();
        string queueKey = GetQueueKey<T>();
        SemaphoreSlim signal = GetOrCreateSignal<T>();

        while (!ct.IsCancellationRequested)
        {
            RedisValue value = await db.ListLeftPopAsync(queueKey);

            if (!value.IsNullOrEmpty)
            {
                JobEnvelope<T>? envelope = JsonSerializer.Deserialize<JobEnvelope<T>>(value!, _jsonOptions);
                if (envelope is not null)
                {
                    string statusKey = GetStatusKey(envelope.JobId);
                    await db.StringSetAsync(statusKey, JobStatus.Running.ToString(), TimeSpan.FromDays(7));

                    _logger.LogInformation("Dequeued job {JobId} of type {JobType} from Redis", envelope.JobId, typeof(T).Name);
                    return envelope.Item;
                }
            }

            // Wait for a signal or poll every 1 second
            try
            {
                await signal.WaitAsync(TimeSpan.FromSeconds(1), ct);
            }
            catch (OperationCanceledException)
            {
                return Error.Unavailable("Dequeue operation was cancelled.");
            }
        }

        return Error.Unavailable("Dequeue operation was cancelled.");
    }

    /// <inheritdoc />
    public async Task<Result<JobStatus>> GetStatusAsync(Guid jobId, CancellationToken ct)
    {
        IDatabase db = _redis.GetDatabase();
        string statusKey = GetStatusKey(jobId);

        RedisValue value = await db.StringGetAsync(statusKey);
        if (value.IsNullOrEmpty)
        {
            return Error.NotFound($"Job {jobId} not found.");
        }

        if (Enum.TryParse<JobStatus>(value!, true, out JobStatus status))
        {
            return status;
        }

        return Error.Internal($"Invalid job status for {jobId}.");
    }

    /// <summary>
    /// Updates the status of a job in Redis.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="status">The new status.</param>
    public async Task UpdateStatusAsync(Guid jobId, JobStatus status)
    {
        IDatabase db = _redis.GetDatabase();
        string statusKey = GetStatusKey(jobId);
        await db.StringSetAsync(statusKey, status.ToString(), TimeSpan.FromDays(7));
        _logger.LogInformation("Job {JobId} status updated to {JobStatus} in Redis", jobId, status);
    }

    private static string GetQueueKey<T>() => $"{QueueKeyPrefix}{typeof(T).Name}";
    private static string GetStatusKey(Guid jobId) => $"{StatusKeyPrefix}{jobId}";

    private SemaphoreSlim GetOrCreateSignal<T>() =>
        _waitSignals.GetOrAdd(typeof(T), _ => new SemaphoreSlim(0));

    private sealed record JobEnvelope<T>(Guid JobId, T Item);
}
