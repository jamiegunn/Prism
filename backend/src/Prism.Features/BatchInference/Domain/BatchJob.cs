namespace Prism.Features.BatchInference.Domain;

/// <summary>
/// Aggregate root representing a batch inference job that processes a dataset through a model.
/// </summary>
public sealed class BatchJob : BaseEntity
{
    /// <summary>
    /// Gets or sets the source dataset ID.
    /// </summary>
    public Guid DatasetId { get; set; }

    /// <summary>
    /// Gets or sets the optional split label to restrict processing to.
    /// </summary>
    public string? SplitLabel { get; set; }

    /// <summary>
    /// Gets or sets the model identifier to use for inference.
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional prompt version ID for prompt formatting.
    /// </summary>
    public Guid? PromptVersionId { get; set; }

    /// <summary>
    /// Gets or sets inference parameters (temperature, top_p, max_tokens, etc.).
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the concurrency limit for parallel inference requests.
    /// </summary>
    public int Concurrency { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum number of retries per failed record.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether to capture logprobs for each inference.
    /// </summary>
    public bool CaptureLogprobs { get; set; }

    /// <summary>
    /// Gets or sets the current job status.
    /// </summary>
    public BatchJobStatus Status { get; set; } = BatchJobStatus.Queued;

    /// <summary>
    /// Gets or sets the progress as a percentage (0.0 to 1.0).
    /// </summary>
    public double Progress { get; set; }

    /// <summary>
    /// Gets or sets the total number of records to process.
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// Gets or sets the number of completed records.
    /// </summary>
    public int CompletedRecords { get; set; }

    /// <summary>
    /// Gets or sets the number of failed records.
    /// </summary>
    public int FailedRecords { get; set; }

    /// <summary>
    /// Gets or sets the total tokens used across all inferences.
    /// </summary>
    public long TokensUsed { get; set; }

    /// <summary>
    /// Gets or sets the estimated cost in USD.
    /// </summary>
    public decimal? Cost { get; set; }

    /// <summary>
    /// Gets or sets when the job started running.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the job finished.
    /// </summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// Gets or sets the output file path for exported results.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Gets or sets the error message if the job failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the navigation collection of results.
    /// </summary>
    public List<BatchResult> Results { get; set; } = [];
}
