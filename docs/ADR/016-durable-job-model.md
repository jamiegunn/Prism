# ADR-016: Durable Job Model

**Date:** 2026-03-16
**Status:** Accepted
**Deciders:** Project team

## Context

Several features require long-running work: batch inference, evaluation runs, document ingestion, embedding jobs, agent execution, and fine-tune job tracking. Without a shared job model, each feature implements ad hoc background processing with inconsistent:

- Progress reporting
- Cancellation handling
- Retry logic
- Failure visibility
- State persistence across restarts

The current approach of using standalone `BackgroundService` or fire-and-forget tasks loses state on process restart and provides no visibility into job progress.

## Decision

Introduce a durable job model in `Prism.Common/Jobs/` with these abstractions:

### Core Interfaces

| Interface | Responsibility |
|-----------|---------------|
| `IJobQueue` | Enqueue job requests, query pending/active jobs |
| `IJobRunner` | Dequeue and execute jobs, manage leases and heartbeats |
| `IJobStore` | Persist job records, state transitions, progress, and artifacts |

### Job Entity

```csharp
public sealed class Job
{
    public Guid JobId { get; init; }
    public string JobType { get; init; }          // e.g., "batch_inference", "evaluation", "rag_ingest"
    public JobStatus Status { get; set; }         // Pending, Running, Paused, Completed, Failed, Cancelled
    public Guid? WorkspaceId { get; init; }
    public Guid? ProjectId { get; init; }
    public JsonDocument Parameters { get; init; } // Job-specific input
    public int Progress { get; set; }             // 0-100
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public int FailedItems { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; init; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? LastHeartbeat { get; set; }
    public Guid? LeaseHolder { get; set; }        // Worker instance ID
    public ICollection<Guid> ArtifactIds { get; set; } // Linked output artifacts
}

public enum JobStatus
{
    Pending,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}
```

### Design Rules

1. **Jobs are durable** — persisted to the database, survive process restarts.
2. **Leases prevent duplicate execution** — a runner acquires a lease with a heartbeat interval. If the heartbeat lapses, the job is eligible for re-acquisition.
3. **Progress is granular** — jobs report item-level progress, not just percent.
4. **Cancellation is cooperative** — jobs check a `CancellationToken` at item boundaries.
5. **Pause/resume where meaningful** — batch jobs can be paused and resumed; atomic jobs (single inference) cannot.
6. **Artifacts are attached** — job outputs are stored as artifacts (ADR-014) and linked via `ArtifactIds`.
7. **Retry policy is per-job-type** — configurable max retries and backoff strategy.

### Implementation Phases

**Phase 1 (in-process):**
- `Channel<T>`-backed queue within the API process
- `BackgroundService`-based runner
- Database-persisted job records
- Heartbeat via timer

**Phase 2 (Redis-backed, Phase 3+):**
- Replace `Channel<T>` with Redis streams or lists
- Multiple worker processes
- Same `IJobQueue`/`IJobRunner` interfaces — no feature rewrites

### Progress Events

Jobs emit progress through `IJobStore.UpdateProgressAsync()`. The API exposes job progress via polling endpoints. SSE-based live progress can be added without changing the job model.

## Consequences

### Positive

- All long-running work has consistent progress, cancellation, and retry behavior
- Jobs survive process restarts (pending/running jobs are re-acquired on startup)
- Visibility into all background work from a single dashboard
- Redis migration is interface-compatible — no feature rewrites
- Artifact attachment provides full provenance for job outputs

### Negative

- Additional complexity for simple one-off background tasks
- Heartbeat management adds code to every job type
- Database overhead for progress updates on high-throughput jobs

### Neutral

- Existing `BackgroundService` usage for non-job work (config watching, health checks) remains unchanged
- Job types are registered via DI — adding a new job type requires implementing `IJobHandler<T>` and registering it
- The in-process implementation is sufficient for single-user local deployment

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| Fire-and-forget BackgroundService | Simple | No persistence, no progress, no retry, lost on restart | Current pain point |
| Hangfire | Full-featured, .NET native | Heavy dependency, SQL Server bias, overkill for local-first | Too much infrastructure for Phase 1 |
| MassTransit + RabbitMQ | Production-grade message bus | Requires RabbitMQ, complex setup, violates "earn your infrastructure" | Over-engineered for single-user |
| Quartz.NET | Mature scheduler | Scheduling focus, not execution/progress focus | Wrong abstraction for long-running jobs |

## References

- ADR-014: Standardized Artifact Model
- Delivery Plan v2: Section 4.E (Move long-running work into a real job model)
- ARCHITECTURE.md: Infrastructure progression philosophy
