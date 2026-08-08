using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Prism.Common.Jobs;

/// <summary>
/// Hosted service that runs the job workers and periodically recovers abandoned jobs.
/// </summary>
/// <remarks>
/// This is the piece whose absence made every queued job permanently inert: Evaluation, Batch,
/// Fine-Tuning and RAG ingestion all created rows that nothing consumed.
/// </remarks>
public sealed class JobWorkerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobWorkerOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<JobWorkerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobWorkerService"/> class.
    /// </summary>
    /// <param name="scopeFactory">Factory for per-job service scopes.</param>
    /// <param name="options">Worker tuning.</param>
    /// <param name="loggerFactory">Factory used to create worker loggers.</param>
    public JobWorkerService(
        IServiceScopeFactory scopeFactory,
        JobWorkerOptions options,
        ILoggerFactory loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<JobWorkerService>();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting {Concurrency} job workers (lease {LeaseTimeout}, heartbeat {HeartbeatInterval})",
            _options.Concurrency, _options.LeaseTimeout, _options.HeartbeatInterval);

        List<Task> loops = Enumerable.Range(0, Math.Max(1, _options.Concurrency))
            .Select(_ => RunWorkerLoopAsync(stoppingToken))
            .ToList();

        loops.Add(RunReclaimLoopAsync(stoppingToken));

        await Task.WhenAll(loops);
    }

    private async Task RunWorkerLoopAsync(CancellationToken ct)
    {
        var worker = new JobWorker(_scopeFactory, _options, _loggerFactory.CreateLogger<JobWorker>());

        while (!ct.IsCancellationRequested)
        {
            try
            {
                Guid? ran = await worker.RunOnceAsync(ct);

                if (ran is null)
                {
                    await Task.Delay(_options.IdlePollInterval, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // The loop itself must not die. A worker that stops on an unexpected error
                // silently drains capacity until someone notices the queue growing.
                _logger.LogError(ex, "Job worker loop error; continuing");
                await SafeDelayAsync(_options.IdlePollInterval, ct);
            }
        }
    }

    private async Task RunReclaimLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.ReclaimInterval, ct);

                await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
                IJobLeaseStore leases = scope.ServiceProvider.GetRequiredService<IJobLeaseStore>();
                await leases.ReclaimExpiredAsync(_options.LeaseTimeout, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job reclaim sweep failed; continuing");
            }
        }
    }

    private static async Task SafeDelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}
