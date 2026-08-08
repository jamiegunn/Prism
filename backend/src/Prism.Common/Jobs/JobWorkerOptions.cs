namespace Prism.Common.Jobs;

/// <summary>
/// Tuning for the background job worker.
/// </summary>
public sealed class JobWorkerOptions
{
    /// <summary>
    /// The configuration section these options bind to.
    /// </summary>
    public const string SectionName = "Jobs:Worker";

    /// <summary>
    /// Gets or sets how many jobs one host runs at a time. Defaults to 2.
    /// </summary>
    public int Concurrency { get; set; } = 2;

    /// <summary>
    /// Gets or sets how often a running job refreshes its lease. Defaults to 15 seconds.
    /// </summary>
    /// <remarks>
    /// Must stay comfortably below <see cref="LeaseTimeout"/>. If a single missed heartbeat
    /// could expire the lease, a slow database moment would hand a running job to a second
    /// worker.
    /// </remarks>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets how long a job may go without a heartbeat before it is presumed abandoned.
    /// Defaults to 2 minutes.
    /// </summary>
    public TimeSpan LeaseTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets how long to wait before checking an empty queue again. Defaults to 2 seconds.
    /// </summary>
    public TimeSpan IdlePollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets how often abandoned jobs are swept back into the queue. Defaults to 1 minute.
    /// </summary>
    public TimeSpan ReclaimInterval { get; set; } = TimeSpan.FromMinutes(1);
}
