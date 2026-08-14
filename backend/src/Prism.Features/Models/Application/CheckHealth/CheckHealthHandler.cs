using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Results;
using Prism.Features.Models.Application.Dtos;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Application.CheckHealth;

/// <summary>
/// Handles health checking of a registered inference provider instance.
/// Updates status, model info, and capabilities in the database.
/// </summary>
public sealed class CheckHealthHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<CheckHealthHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckHealthHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="providerFactory">The factory for creating inference provider instances.</param>
    /// <param name="logger">The logger instance.</param>
    public CheckHealthHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ILogger<CheckHealthHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Performs a health check on the specified instance, updating its status and capabilities in the database.
    /// </summary>
    /// <param name="command">The command containing the instance ID to check.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the updated instance DTO on success.</returns>
    public async Task<Result<InferenceInstanceDto>> HandleAsync(CheckHealthCommand command, CancellationToken ct)
    {
        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .FirstOrDefaultAsync(i => i.Id == command.InstanceId, ct);

        if (instance is null)
        {
            return Error.NotFound($"Inference instance with ID '{command.InstanceId}' was not found.");
        }

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        InstanceStatus previousStatus = instance.Status;

        try
        {
            Result<HealthStatus> healthResult = await provider.CheckHealthAsync(ct);
            if (healthResult.IsSuccess)
            {
                HealthStatus health = healthResult.Value;
                instance.Status = health.IsHealthy ? InstanceStatus.Online : InstanceStatus.Offline;
                instance.LastHealthCheck = health.LastCheckAt;
                instance.LastHealthError = health.ErrorMessage;
            }
            else
            {
                instance.Status = InstanceStatus.Offline;
                instance.LastHealthCheck = DateTime.UtcNow;
                instance.LastHealthError = healthResult.Error.Message;
            }

            Result<ModelInfo> modelResult = await provider.GetModelInfoAsync(ct);
            if (modelResult.IsSuccess)
            {
                ModelInfo modelInfo = modelResult.Value;

                if (await ShouldAdoptModelAsync(provider, instance, ct))
                {
                    instance.ModelId = modelInfo.ModelId;
                }

                instance.MaxContextLength = modelInfo.MaxContextLength;
                instance.SupportsLogprobs = modelInfo.Capabilities.SupportsLogprobs;
                instance.MaxTopLogprobs = modelInfo.Capabilities.MaxTopLogprobs;
                instance.SupportsStreaming = modelInfo.Capabilities.SupportsStreaming;
                instance.SupportsMetrics = modelInfo.Capabilities.SupportsMetrics;
                instance.SupportsTokenize = modelInfo.Capabilities.SupportsTokenize;
                instance.SupportsGuidedDecoding = modelInfo.Capabilities.SupportsGuidedDecoding;
                instance.SupportsModelSwap = modelInfo.Capabilities.SupportsHotReload;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for instance {InstanceId}", command.InstanceId);
            instance.Status = InstanceStatus.Offline;
            instance.LastHealthCheck = DateTime.UtcNow;
            instance.LastHealthError = ex.Message;
        }

        instance.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (previousStatus != instance.Status)
        {
            _logger.LogInformation(
                "Instance {InstanceName} ({InstanceId}) status changed from {PreviousStatus} to {NewStatus}",
                instance.Name, instance.Id, previousStatus, instance.Status);
        }

        return InferenceInstanceDto.FromEntity(instance);
    }

    /// <summary>
    /// Decides whether a health check may replace the model an instance is set to.
    /// </summary>
    /// <param name="provider">The provider being checked.</param>
    /// <param name="instance">The instance as stored.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when the instance has no model, or its model is definitely not on
    /// the server any more.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This used to be unconditional, and for Ollama the server's answer is "whatever was pulled
    /// most recently" — so a routine poll reassigned every instance whenever anything was
    /// downloaded. Pulling an embedding model to make RAG work took every generative screen down
    /// with <c>does not support chat</c>.
    /// </para>
    /// <para>
    /// The one case worth overruling the stored value is a model that is genuinely gone, where
    /// keeping it points at nothing. That needs the server's list, so a provider that cannot list
    /// models keeps its instance's model untouched: an unanswered question is not evidence the
    /// model disappeared.
    /// </para>
    /// </remarks>
    private async Task<bool> ShouldAdoptModelAsync(
        IInferenceProvider provider, InferenceInstance instance, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(instance.ModelId))
        {
            return true;
        }

        // A model that cannot generate text is not a choice worth protecting — nothing in Prism
        // asks an instance for an embedding, so this value can only ever fail. Installs that ran
        // under the old health check are sitting on exactly that, and this is what repairs them.
        bool? canChat = await (provider.As<IModelPurposeProbe>()?.CanGenerateTextAsync(instance.ModelId, ct)
                               ?? Task.FromResult<bool?>(null));

        if (canChat is false)
        {
            _logger.LogInformation(
                "Instance {InstanceName} was set to {ModelId}, which cannot generate text; adopting a model that can",
                instance.Name, instance.ModelId);

            return true;
        }

        IHotReloadableProvider? lister = provider.As<IHotReloadableProvider>();
        if (lister is null)
        {
            return false;
        }

        Result<IReadOnlyList<AvailableModel>> models = await lister.ListAvailableModelsAsync(ct);
        if (models.IsFailure || models.Value.Count == 0)
        {
            return false;
        }

        bool stillThere = models.Value.Any(m =>
            string.Equals(m.ModelId, instance.ModelId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(m.Name, instance.ModelId, StringComparison.OrdinalIgnoreCase));

        if (!stillThere)
        {
            _logger.LogInformation(
                "Instance {InstanceName} was set to {ModelId}, which {Endpoint} no longer has; adopting a model it does",
                instance.Name, instance.ModelId, instance.Endpoint);
        }

        return !stillThere;
    }
}
