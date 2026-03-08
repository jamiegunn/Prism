namespace Prism.Features.Models.Application.UnregisterInstance;

/// <summary>
/// Command to unregister (remove) an inference provider instance.
/// </summary>
/// <param name="Id">The unique identifier of the instance to unregister.</param>
public sealed record UnregisterInstanceCommand(Guid Id);
