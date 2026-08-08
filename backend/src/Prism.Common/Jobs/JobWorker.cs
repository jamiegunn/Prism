using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Prism.Common.Jobs;

/// <summary>
/// Claims a single job, runs its handler while keeping the lease alive, and records the outcome.
/// </summary>
/// <remarks>
/// Separated from the hosted service so the whole claim-execute-complete cycle can be driven
/// one step at a time from a test. A worker that can only be observed through a background
/// loop is a worker whose failure modes are tested by waiting and hoping.
/// </remarks>
public sealed class JobWorker
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobWorkerOptions _options;
    private readonly ILogger<JobWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobWorker"/> class.
    /// </summary>
    /// <param name="scopeFactory">Factory for per-job service scopes.</param>
    /// <param name="options">Worker tuning.</param>
    /// <param name="logger">The logger instance.</param>
    public JobWorker(
        IServiceScopeFactory scopeFactory,
        JobWorkerOptions options,
        ILogger<JobWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Claims and runs at most one job.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>
    /// The identifier of the job that ran, or <see langword="null"/> if the queue held nothing
    /// this worker could execute.
    /// </returns>
    public async Task<Guid?> RunOnceAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        IJobLeaseStore leases = scope.ServiceProvider.GetRequiredService<IJobLeaseStore>();
        Dictionary<string, IJobHandler> handlers = scope.ServiceProvider
            .GetServices<IJobHandler>()
            .ToDictionary(h => h.JobType, StringComparer.Ordinal);

        if (handlers.Count == 0)
        {
            return null;
        }

        DurableJob? job = await leases.ClaimNextAsync([.. handlers.Keys], ct);

        if (job is null)
        {
            return null;
        }

        string? failure = null;

        // The heartbeat runs on its own scope, and so its own DbContext: EF contexts are not
        // safe for concurrent use, and this one has to write while the handler is working.
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task heartbeat = KeepLeaseAliveAsync(job.Id, heartbeatCts.Token);

        try
        {
            await handlers[job.JobType].ExecuteAsync(job, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown, not failure. Leave the job alone: its lease will expire and another
            // worker will pick it up. Marking it failed here would burn a retry for what is
            // really a deployment.
            _logger.LogInformation("Job {JobId} interrupted by shutdown; leaving it to be reclaimed", job.Id);
            await StopHeartbeatAsync(heartbeatCts, heartbeat);
            return job.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} of type {JobType} failed", job.Id, job.JobType);
            failure = ex.Message;
        }

        await StopHeartbeatAsync(heartbeatCts, heartbeat);

        await using AsyncServiceScope completionScope = _scopeFactory.CreateAsyncScope();
        IJobLeaseStore completionLeases =
            completionScope.ServiceProvider.GetRequiredService<IJobLeaseStore>();

        await completionLeases.CompleteAsync(job.Id, failure, CancellationToken.None);

        return job.Id;
    }

    private static async Task StopHeartbeatAsync(CancellationTokenSource cts, Task heartbeat)
    {
        await cts.CancelAsync();

        try
        {
            await heartbeat;
        }
        catch (OperationCanceledException)
        {
            // Expected: this is how the heartbeat loop ends.
        }
    }

    private async Task KeepLeaseAliveAsync(Guid jobId, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.HeartbeatInterval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
                IJobStore store = scope.ServiceProvider.GetRequiredService<IJobStore>();
                await store.HeartbeatAsync(jobId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A missed heartbeat is survivable — the lease is longer than the interval.
                // Letting this throw would tear down the job for a transient database blip.
                _logger.LogWarning(ex, "Heartbeat failed for job {JobId}", jobId);
            }
        }
    }
}
