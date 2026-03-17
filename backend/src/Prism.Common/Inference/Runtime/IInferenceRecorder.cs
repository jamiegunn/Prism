using Prism.Common.Inference.Models;
using Prism.Common.Results;

namespace Prism.Common.Inference.Runtime;

/// <summary>
/// Records inference runs for history and traceability.
/// Abstracts the recording mechanism so the runtime doesn't depend on channels or specific persistence.
/// </summary>
public interface IInferenceRecorder
{
    /// <summary>
    /// Records a completed inference run.
    /// </summary>
    /// <param name="record">The inference record data to persist.</param>
    /// <param name="options">Runtime options containing tags and attribution metadata.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating whether the record was accepted for persistence.</returns>
    Task<Result> RecordAsync(InferenceRecordData record, InferenceRunOptions options, CancellationToken ct);
}
