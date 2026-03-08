namespace Prism.Features.History.Application.ReplaySingle;

/// <summary>
/// Command to replay a previously recorded inference request against a specified provider instance.
/// Allows comparing the original response with a new response from a different model or provider.
/// </summary>
/// <param name="RecordId">The unique identifier of the original inference record to replay.</param>
/// <param name="InstanceId">The unique identifier of the inference instance to send the replay to.</param>
public sealed record ReplaySingleCommand(Guid RecordId, Guid InstanceId);
