using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Api.Requests;

/// <summary>
/// HTTP request body for updating dataset metadata.
/// </summary>
/// <param name="Name">The new name.</param>
/// <param name="Description">The new description.</param>
/// <param name="Schema">The updated column schema (optional).</param>
public sealed record UpdateDatasetRequest(string Name, string? Description, List<ColumnSchema>? Schema);
