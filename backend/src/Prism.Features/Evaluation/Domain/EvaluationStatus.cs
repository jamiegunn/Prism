namespace Prism.Features.Evaluation.Domain;

/// <summary>
/// Represents the execution status of an evaluation.
/// </summary>
public enum EvaluationStatus
{
    /// <summary>The evaluation is pending execution.</summary>
    Pending,

    /// <summary>The evaluation is currently running.</summary>
    Running,

    /// <summary>The evaluation has been paused.</summary>
    Paused,

    /// <summary>The evaluation completed successfully.</summary>
    Completed,

    /// <summary>The evaluation failed with an error.</summary>
    Failed,

    /// <summary>The evaluation was cancelled by the user.</summary>
    Cancelled
}
