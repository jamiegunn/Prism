namespace Prism.Features.BatchInference.Api.Requests;

/// <summary>
/// HTTP request body for creating a new batch inference job.
/// </summary>
public sealed record CreateBatchJobRequest(
    Guid DatasetId,
    string? SplitLabel,
    string Model,
    Guid? PromptVersionId,
    Dictionary<string, object?>? Parameters,
    int Concurrency,
    int MaxRetries,
    bool CaptureLogprobs);
