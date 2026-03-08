namespace Prism.Features.Evaluation.Domain;

/// <summary>
/// Aggregate root representing an evaluation run that scores model outputs against a dataset.
/// </summary>
public sealed class EvaluationEntity : BaseEntity
{
    /// <summary>
    /// Gets or sets the optional project this evaluation belongs to.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the dataset to evaluate against.
    /// </summary>
    public Guid DatasetId { get; set; }

    /// <summary>
    /// Gets or sets the optional split label to restrict evaluation to (e.g., "test").
    /// </summary>
    public string? SplitLabel { get; set; }

    /// <summary>
    /// Gets or sets the display name of this evaluation.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the list of model identifiers to evaluate.
    /// </summary>
    public List<string> Models { get; set; } = [];

    /// <summary>
    /// Gets or sets the optional prompt version ID to use for generating prompts.
    /// </summary>
    public Guid? PromptVersionId { get; set; }

    /// <summary>
    /// Gets or sets the scoring method names to apply (e.g., "exact_match", "rouge_l", "bleu").
    /// </summary>
    public List<string> ScoringMethods { get; set; } = [];

    /// <summary>
    /// Gets or sets additional configuration as key-value pairs (e.g., temperature, max_tokens).
    /// </summary>
    public Dictionary<string, object?> Config { get; set; } = new();

    /// <summary>
    /// Gets or sets the current execution status.
    /// </summary>
    public EvaluationStatus Status { get; set; } = EvaluationStatus.Pending;

    /// <summary>
    /// Gets or sets the progress as a percentage (0.0 to 1.0).
    /// </summary>
    public double Progress { get; set; }

    /// <summary>
    /// Gets or sets the total number of records to evaluate.
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
    /// Gets or sets the error message if the evaluation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets when the evaluation started running.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the evaluation finished.
    /// </summary>
    public DateTime? FinishedAt { get; set; }

    /// <summary>
    /// Gets or sets the navigation collection of results.
    /// </summary>
    public List<EvaluationResult> Results { get; set; } = [];
}
