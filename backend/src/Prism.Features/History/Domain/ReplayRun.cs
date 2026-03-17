using Prism.Common.Database;

namespace Prism.Features.History.Domain;

/// <summary>
/// Links a replay execution to its original inference record.
/// Tracks what overrides were applied and provides bidirectional navigation
/// between the original run and its replays.
/// </summary>
public sealed class ReplayRun : BaseEntity
{
    /// <summary>
    /// Gets or sets the ID of the original inference record that was replayed.
    /// </summary>
    public Guid OriginalRecordId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the original record.
    /// </summary>
    public InferenceRecord? OriginalRecord { get; set; }

    /// <summary>
    /// Gets or sets the ID of the new inference record produced by the replay.
    /// </summary>
    public Guid ReplayRecordId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the replay's inference record.
    /// </summary>
    public InferenceRecord? ReplayRecord { get; set; }

    /// <summary>
    /// Gets or sets the model override applied during replay, or null if unchanged.
    /// </summary>
    public string? OverrideModel { get; set; }

    /// <summary>
    /// Gets or sets the temperature override applied during replay, or null if unchanged.
    /// </summary>
    public double? OverrideTemperature { get; set; }

    /// <summary>
    /// Gets or sets the max tokens override applied during replay, or null if unchanged.
    /// </summary>
    public int? OverrideMaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the top-P override applied during replay, or null if unchanged.
    /// </summary>
    public double? OverrideTopP { get; set; }

    /// <summary>
    /// Gets or sets the provider instance override ID, or null if unchanged.
    /// </summary>
    public Guid? OverrideInstanceId { get; set; }
}
