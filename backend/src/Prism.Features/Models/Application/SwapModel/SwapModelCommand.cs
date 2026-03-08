namespace Prism.Features.Models.Application.SwapModel;

/// <summary>
/// Command to swap the currently loaded model on an inference instance.
/// </summary>
/// <param name="InstanceId">The unique identifier of the instance.</param>
/// <param name="ModelId">The identifier of the model to load.</param>
public sealed record SwapModelCommand(Guid InstanceId, string ModelId);
