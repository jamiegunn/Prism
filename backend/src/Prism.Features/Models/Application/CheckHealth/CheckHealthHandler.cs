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
                instance.ModelId = modelInfo.ModelId;
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
}
