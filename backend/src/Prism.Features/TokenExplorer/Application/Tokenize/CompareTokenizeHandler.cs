using Microsoft.EntityFrameworkCore;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;
using Prism.Features.TokenExplorer.Application.Dtos;

namespace Prism.Features.TokenExplorer.Application.Tokenize;

/// <summary>
/// Handles tokenization of the same text across multiple inference instances for comparison.
/// Processes all instances in parallel and collects results, including partial failures.
/// </summary>
public sealed class CompareTokenizeHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<CompareTokenizeHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompareTokenizeHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="providerFactory">The factory for creating inference provider instances.</param>
    /// <param name="logger">The logger instance.</param>
    public CompareTokenizeHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ILogger<CompareTokenizeHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Tokenizes the given text across all specified inference instances in parallel.
    /// Individual instance failures are captured in the result rather than failing the whole request.
    /// </summary>
    /// <param name="command">The compare tokenize command containing instance IDs and text.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the comparison results on success, or an error on failure.</returns>
    public async Task<Result<CompareTokenizeResultDto>> HandleAsync(CompareTokenizeCommand command, CancellationToken ct)
    {
        if (command.InstanceIds.Count == 0)
        {
            return Error.Validation("At least one instance ID must be provided.");
        }

        List<InferenceInstance> instances = await _db.Set<InferenceInstance>()
            .AsNoTracking()
            .Where(i => command.InstanceIds.Contains(i.Id))
            .ToListAsync(ct);

        if (instances.Count == 0)
        {
            return Error.NotFound("None of the specified inference instances were found.");
        }

        IEnumerable<Task<InstanceTokenizeResult>> tasks = instances.Select(
            instance => TokenizeForInstanceAsync(instance, command.Text, ct));

        InstanceTokenizeResult[] results = await Task.WhenAll(tasks);

        return new CompareTokenizeResultDto(command.Text, results);
    }

    /// <summary>
    /// Tokenizes text for a single inference instance, catching errors and returning them
    /// as part of the result rather than throwing.
    /// </summary>
    /// <param name="instance">The inference instance to tokenize with.</param>
    /// <param name="text">The text to tokenize.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The tokenization result for this instance, including any error message.</returns>
    private async Task<InstanceTokenizeResult> TokenizeForInstanceAsync(
        InferenceInstance instance, string text, CancellationToken ct)
    {
        string modelId = instance.ModelId ?? "";

        try
        {
            IInferenceProvider provider = _providerFactory.CreateProvider(
                instance.Name, instance.Endpoint, instance.ProviderType);

            Result<TokenizeResponse> tokenizeResult = await provider.TokenizeAsync(text, ct);

            if (tokenizeResult.IsFailure)
            {
                _logger.LogWarning(
                    "Tokenization failed for instance {InstanceId} ({InstanceName}): {ErrorMessage}",
                    instance.Id, instance.Name, tokenizeResult.Error.Message);

                return new InstanceTokenizeResult(
                    instance.Id, instance.Name, modelId, null, tokenizeResult.Error.Message);
            }

            TokenizeResultDto dto = TokenizeHandler.BuildTokenizeResultDto(
                tokenizeResult.Value, text, modelId);

            return new InstanceTokenizeResult(instance.Id, instance.Name, modelId, dto, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during tokenization for instance {InstanceId} ({InstanceName})",
                instance.Id, instance.Name);

            return new InstanceTokenizeResult(
                instance.Id, instance.Name, modelId, null, $"Unexpected error: {ex.Message}");
        }
    }
}
