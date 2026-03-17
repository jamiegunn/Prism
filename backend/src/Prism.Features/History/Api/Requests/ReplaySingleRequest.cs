namespace Prism.Features.History.Api.Requests;

/// <summary>
/// Request body for replaying an inference record against a specified provider instance.
/// Supports optional parameter overrides to test how changes affect the response.
/// </summary>
/// <param name="InstanceId">The unique identifier of the inference instance to replay against.</param>
/// <param name="OverrideModel">Optional model override. If null, uses the target instance's model.</param>
/// <param name="OverrideTemperature">Optional temperature override.</param>
/// <param name="OverrideMaxTokens">Optional max tokens override.</param>
/// <param name="OverrideTopP">Optional top-P override.</param>
public sealed record ReplaySingleRequest(
    Guid InstanceId,
    string? OverrideModel = null,
    double? OverrideTemperature = null,
    int? OverrideMaxTokens = null,
    double? OverrideTopP = null);
