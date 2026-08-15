using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
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
        // Checked before anything is contacted. An empty field used to reach Ollama and come back
        // as a 503 quoting its `invalid model name`, which reads as the server being unwell
        // rather than as a box nobody filled in.
        if (string.IsNullOrWhiteSpace(command.ModelId))
        {
            return Error.Validation("Name the model to switch to.");
        }

        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .FirstOrDefaultAsync(i => i.Id == command.InstanceId, ct);

        if (instance is null)
        {
            return Error.NotFound($"Inference instance with ID '{command.InstanceId}' was not found.");
        }

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        // Asked through the decorator chain: recording wraps every provider the factory
        // builds, and a direct type test therefore answered "no" for every provider there is.
        IHotReloadableProvider? hotReloadable = provider.As<IHotReloadableProvider>();

        if (hotReloadable is null)
        {
            return Error.Unavailable(
                $"Provider type '{instance.ProviderType}' does not support hot-swapping models.");
        }

        // What the server actually has, so a typo is caught here rather than by every request
        // made afterwards. A pull of a model that does not exist streams its error inside a 200,
        // and while that is now read properly, being told "this server has no such model" is a
        // better answer than a pull failure — and it is the only way to offer the alternatives.
        Result<IReadOnlyList<AvailableModel>> available = await hotReloadable.ListAvailableModelsAsync(ct);

        if (available.IsSuccess && available.Value.Count > 0)
        {
            bool present = available.Value.Any(m =>
                string.Equals(m.ModelId, command.ModelId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(m.Name, command.ModelId, StringComparison.OrdinalIgnoreCase));

            if (!present)
            {
                return Error.Validation(
                    $"'{instance.Name}' has no model called '{command.ModelId}'. " +
                    $"It has: {string.Join(", ", available.Value.Select(m => m.ModelId))}.");
            }
        }

        // An instance is asked to hold conversations, so a model that only embeds can never
        // serve one. Allowing it produced exactly the breakage the health check now repairs.
        bool? canChat = await (provider.As<IModelPurposeProbe>()?.CanGenerateTextAsync(command.ModelId, ct)
                               ?? Task.FromResult<bool?>(null));

        if (canChat is false)
        {
            return Error.Validation(
                $"'{command.ModelId}' is an embedding model and cannot generate text, so an " +
                "instance cannot run it. Choose a chat model.");
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
