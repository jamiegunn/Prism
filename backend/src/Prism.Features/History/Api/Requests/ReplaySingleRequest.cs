namespace Prism.Features.History.Api.Requests;

/// <summary>
/// Request body for replaying an inference record against a specified provider instance.
/// </summary>
/// <param name="InstanceId">The unique identifier of the inference instance to replay against.</param>
public sealed record ReplaySingleRequest(Guid InstanceId);
