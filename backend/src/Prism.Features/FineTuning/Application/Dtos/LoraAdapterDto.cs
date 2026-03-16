using Prism.Features.FineTuning.Domain;

namespace Prism.Features.FineTuning.Application.Dtos;

/// <summary>
/// Data transfer object for a LoRA adapter.
/// </summary>
public sealed record LoraAdapterDto(
    Guid Id,
    string Name,
    string? Description,
    Guid InstanceId,
    string AdapterPath,
    string BaseModel,
    bool IsActive,
    DateTime CreatedAt)
{
    /// <summary>
    /// Creates a DTO from a <see cref="LoraAdapter"/> entity.
    /// </summary>
    /// <param name="entity">The adapter entity.</param>
    /// <returns>A new <see cref="LoraAdapterDto"/>.</returns>
    public static LoraAdapterDto FromEntity(LoraAdapter entity) => new(
        entity.Id,
        entity.Name,
        entity.Description,
        entity.InstanceId,
        entity.AdapterPath,
        entity.BaseModel,
        entity.IsActive,
        entity.CreatedAt);
}
