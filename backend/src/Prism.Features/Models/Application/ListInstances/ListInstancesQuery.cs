using Prism.Common.Inference;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Application.ListInstances;

/// <summary>
/// Query to list registered inference provider instances with optional filters.
/// </summary>
/// <param name="Status">Optional filter by instance status.</param>
/// <param name="ProviderType">Optional filter by provider type.</param>
public sealed record ListInstancesQuery(
    InstanceStatus? Status = null,
    InferenceProviderType? ProviderType = null);
