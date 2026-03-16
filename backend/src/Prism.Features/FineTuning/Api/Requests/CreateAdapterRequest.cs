namespace Prism.Features.FineTuning.Api.Requests;

/// <summary>
/// Request to register a new LoRA adapter.
/// </summary>
public sealed record CreateAdapterRequest(
    string Name,
    string? Description,
    Guid InstanceId,
    string AdapterPath,
    string BaseModel);
