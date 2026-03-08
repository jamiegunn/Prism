using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Results;
using Prism.Features.Models.Application.Dtos;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Application.SwapModel;

/// <summary>
/// Handles hot-swapping the model loaded on an inference instance.
/// Requires the provider to implement <see cref="IHotReloadableProvider"/>.
/// </summary>
public sealed class SwapModelHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<SwapModelHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwapModelHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="providerFactory">The factory for creating inference provider instances.</param>
    /// <param name="logger">The logger instance.</param>
    public SwapModelHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ILogger<SwapModelHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Swaps the currently loaded model on the specified instance.
    /// Returns an error if the provider does not support hot-reloading.
    /// </summary>
    /// <param name="command">The command containing the instance ID and target model.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the updated instance DTO on success.</returns>
    public async Task<Result<InferenceInstanceDto>> HandleAsync(SwapModelCommand command, CancellationToken ct)
    {
        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .FirstOrDefaultAsync(i => i.Id == command.InstanceId, ct);

        if (instance is null)
        {
            return Error.NotFound($"Inference instance with ID '{command.InstanceId}' was not found.");
        }

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        if (provider is not IHotReloadableProvider hotReloadable)
        {
            return Error.Unavailable(
                $"Provider type '{instance.ProviderType}' does not support hot-swapping models.");
        }

        _logger.LogInformation("Swapping model on instance {InstanceName} ({InstanceId}) to {ModelId}",
            instance.Name, instance.Id, command.ModelId);

        Result loadResult = await hotReloadable.LoadModelAsync(command.ModelId, ct);
        if (loadResult.IsFailure)
        {
            return Error.Unavailable($"Failed to load model '{command.ModelId}': {loadResult.Error.Message}");
        }

        instance.ModelId = command.ModelId;
        instance.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Successfully swapped model on instance {InstanceName} to {ModelId}",
            instance.Name, command.ModelId);

        return InferenceInstanceDto.FromEntity(instance);
    }
}
