using System.Collections.Concurrent;
using System.Threading.Channels;
using Prism.Common.Results;

namespace Prism.Common.Jobs;

/// <summary>
/// In-memory implementation of <see cref="IJobQueue"/> using <see cref="Channel{T}"/>.
/// Suitable for single-instance deployments (Phase 1-2). Job state is lost on application restart.
/// </summary>
public sealed class InMemoryJobQueue : IJobQueue
{
    private readonly ConcurrentDictionary<Guid, JobStatus> _jobStatuses = new();
    private readonly ConcurrentDictionary<Type, object> _channels = new();
    private readonly ILogger<InMemoryJobQueue> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryJobQueue"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public InMemoryJobQueue(ILogger<InMemoryJobQueue> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Enqueues a work item to the in-memory channel for the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the work item payload.</typeparam>
    /// <param name="item">The work item to enqueue.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the job identifier.</returns>
    public async Task<Result<Guid>> EnqueueAsync<T>(T item, CancellationToken ct) where T : class
    {
        Guid jobId = Guid.NewGuid();
        Channel<(Guid, T)> channel = GetOrCreateChannel<T>();

        _jobStatuses[jobId] = JobStatus.Queued;

        await channel.Writer.WriteAsync((jobId, item), ct);
        _logger.LogInformation("Enqueued job {JobId} of type {JobType}", jobId, typeof(T).Name);

        return jobId;
    }

    /// <summary>
    /// Dequeues the next available work item from the in-memory channel.
    /// Blocks until an item is available or cancellation is requested.
    /// </summary>
    /// <typeparam name="T">The type of the work item payload.</typeparam>
    /// <param name="ct">A token to cancel the wait.</param>
    /// <returns>A result containing the dequeued work item.</returns>
    public async Task<Result<T>> DequeueAsync<T>(CancellationToken ct) where T : class
    {
        Channel<(Guid, T)> channel = GetOrCreateChannel<T>();

        try
        {
            (Guid jobId, T item) = await channel.Reader.ReadAsync(ct);
            _jobStatuses[jobId] = JobStatus.Running;
            _logger.LogInformation("Dequeued job {JobId} of type {JobType}", jobId, typeof(T).Name);
            return item;
        }
        catch (OperationCanceledException)
        {
            return Error.Unavailable("Dequeue operation was cancelled.");
        }
    }

    /// <summary>
    /// Gets the status of a job by its identifier.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the job status, or NotFound if the job does not exist.</returns>
    public Task<Result<JobStatus>> GetStatusAsync(Guid jobId, CancellationToken ct)
    {
        if (_jobStatuses.TryGetValue(jobId, out JobStatus status))
        {
            return Task.FromResult<Result<JobStatus>>(status);
        }

        return Task.FromResult<Result<JobStatus>>(Error.NotFound($"Job {jobId} not found."));
    }

    /// <summary>
    /// Updates the status of a job. Used by job processors to mark jobs as complete or failed.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="status">The new status.</param>
    public void UpdateStatus(Guid jobId, JobStatus status)
    {
        _jobStatuses[jobId] = status;
        _logger.LogInformation("Job {JobId} status updated to {JobStatus}", jobId, status);
    }

    private Channel<(Guid, T)> GetOrCreateChannel<T>() where T : class
    {
        return (Channel<(Guid, T)>)_channels.GetOrAdd(
            typeof(T),
            _ => Channel.CreateUnbounded<(Guid, T)>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            }));
    }
}
