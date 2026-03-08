using Microsoft.EntityFrameworkCore;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;
using Prism.Features.TokenExplorer.Application.Dtos;

namespace Prism.Features.TokenExplorer.Application.Tokenize;

/// <summary>
/// Handles detokenization of token IDs back to text using a specific inference instance.
/// </summary>
public sealed class DetokenizeHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<DetokenizeHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DetokenizeHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="providerFactory">The factory for creating inference provider instances.</param>
    /// <param name="logger">The logger instance.</param>
    public DetokenizeHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ILogger<DetokenizeHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Detokenizes the given token IDs using the specified inference instance.
    /// </summary>
    /// <param name="command">The detokenize command containing the instance ID and token IDs.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the decoded text on success, or an error on failure.</returns>
    public async Task<Result<DetokenizeResultDto>> HandleAsync(DetokenizeCommand command, CancellationToken ct)
    {
        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == command.InstanceId, ct);

        if (instance is null)
        {
            return Error.NotFound($"Inference instance '{command.InstanceId}' was not found.");
        }

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        Result<DetokenizeResponse> detokenizeResult = await provider.DetokenizeAsync(command.TokenIds, ct);

        if (detokenizeResult.IsFailure)
        {
            _logger.LogWarning(
                "Detokenization failed for instance {InstanceId}: {ErrorMessage}",
                command.InstanceId, detokenizeResult.Error.Message);
            return detokenizeResult.Error;
        }

        return new DetokenizeResultDto(
            detokenizeResult.Value.Text,
            command.TokenIds,
            instance.ModelId ?? "");
    }
}
