namespace Prism.Features.History.Application.ReplaySingle;

/// <summary>
/// Command to replay a previously recorded inference request against a specified provider instance.
/// Supports optional parameter overrides for A/B testing and sensitivity analysis.
/// </summary>
/// <param name="RecordId">The unique identifier of the original inference record to replay.</param>
/// <param name="InstanceId">The unique identifier of the inference instance to send the replay to.</param>
/// <param name="OverrideModel">Optional model override.</param>
/// <param name="OverrideTemperature">Optional temperature override.</param>
/// <param name="OverrideMaxTokens">Optional max tokens override.</param>
/// <param name="OverrideTopP">Optional top-P override.</param>
public sealed record ReplaySingleCommand(
    Guid RecordId,
    Guid InstanceId,
    string? OverrideModel = null,
    double? OverrideTemperature = null,
    int? OverrideMaxTokens = null,
    double? OverrideTopP = null);
