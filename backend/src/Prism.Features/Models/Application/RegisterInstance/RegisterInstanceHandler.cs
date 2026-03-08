using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Results;
using Prism.Features.Models.Application.Dtos;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Application.RegisterInstance;

/// <summary>
/// Handles registration of a new inference provider instance.
/// Auto-detects model info and capabilities by probing the provider endpoint.
/// </summary>
public sealed class RegisterInstanceHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly IValidator<RegisterInstanceCommand> _validator;
    private readonly ILogger<RegisterInstanceHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterInstanceHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="providerFactory">The factory for creating inference provider instances.</param>
    /// <param name="validator">The validator for the registration command.</param>
    /// <param name="logger">The logger instance.</param>
    public RegisterInstanceHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        IValidator<RegisterInstanceCommand> validator,
        ILogger<RegisterInstanceHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new inference provider instance, probing the endpoint for model and capability information.
    /// </summary>
    /// <param name="command">The registration command containing instance details.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the registered instance DTO on success.</returns>
    public async Task<Result<InferenceInstanceDto>> HandleAsync(RegisterInstanceCommand command, CancellationToken ct)
    {
        ValidationResult validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            string errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Error.Validation(errors);
        }

        var instance = new InferenceInstance
        {
            Name = command.Name,
            Endpoint = command.Endpoint,
            ProviderType = command.ProviderType,
            IsDefault = command.IsDefault,
            Tags = command.Tags ?? [],
            Status = InstanceStatus.Unknown
        };

        // Probe the provider to auto-detect capabilities and model info
        IInferenceProvider provider = _providerFactory.CreateProvider(
            command.Name, command.Endpoint, command.ProviderType);

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
            else
            {
                // Use static capabilities from the provider even if model info probe failed
                ProviderCapabilities caps = provider.Capabilities;
                instance.SupportsLogprobs = caps.SupportsLogprobs;
                instance.MaxTopLogprobs = caps.MaxTopLogprobs;
                instance.SupportsStreaming = caps.SupportsStreaming;
                instance.SupportsMetrics = caps.SupportsMetrics;
                instance.SupportsTokenize = caps.SupportsTokenize;
                instance.SupportsGuidedDecoding = caps.SupportsGuidedDecoding;
                instance.SupportsModelSwap = caps.SupportsHotReload;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to probe provider at {Endpoint} during registration", command.Endpoint);
            instance.Status = InstanceStatus.Offline;
            instance.LastHealthError = ex.Message;
            instance.LastHealthCheck = DateTime.UtcNow;

            // Fallback to static capabilities
            ProviderCapabilities caps = provider.Capabilities;
            instance.SupportsLogprobs = caps.SupportsLogprobs;
            instance.MaxTopLogprobs = caps.MaxTopLogprobs;
            instance.SupportsStreaming = caps.SupportsStreaming;
            instance.SupportsMetrics = caps.SupportsMetrics;
            instance.SupportsTokenize = caps.SupportsTokenize;
            instance.SupportsGuidedDecoding = caps.SupportsGuidedDecoding;
            instance.SupportsModelSwap = caps.SupportsHotReload;
        }

        // If IsDefault, clear default on all other instances
        if (command.IsDefault)
        {
            await _db.Set<InferenceInstance>()
                .Where(i => i.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsDefault, false), ct);
        }

        _db.Set<InferenceInstance>().Add(instance);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Registered inference instance {InstanceName} at {Endpoint} with status {Status}",
            instance.Name, instance.Endpoint, instance.Status);

        return InferenceInstanceDto.FromEntity(instance);
    }
}
