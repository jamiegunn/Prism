using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Inference.Runtime;
using Prism.Common.Results;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Infrastructure;

/// <summary>
/// Resolves inference providers by loading instance metadata from the database
/// and delegating to <see cref="InferenceProviderFactory"/> for creation.
/// </summary>
public sealed class RuntimeProviderResolver : IRuntimeProviderResolver
{
    private readonly AppDbContext _context;
    private readonly InferenceProviderFactory _providerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuntimeProviderResolver"/> class.
    /// </summary>
    /// <param name="context">The database context for loading instance records.</param>
    /// <param name="providerFactory">The factory for creating provider instances.</param>
    public RuntimeProviderResolver(AppDbContext context, InferenceProviderFactory providerFactory)
    {
        _context = context;
        _providerFactory = providerFactory;
    }

    /// <inheritdoc />
    public async Task<Result<IInferenceProvider>> ResolveAsync(Guid instanceId, CancellationToken ct)
    {
        InferenceInstance? instance = await _context.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct);

        if (instance is null)
        {
            return Result<IInferenceProvider>.Failure(
                Error.NotFound($"Inference instance {instanceId} not found."));
        }

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        return Result<IInferenceProvider>.Success(provider);
    }
}
