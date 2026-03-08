using Microsoft.EntityFrameworkCore;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Features.Models.Application.Dtos;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Application.GetTokenizerInfo;

/// <summary>
/// Handles retrieval of tokenizer information for a specific inference instance.
/// </summary>
public sealed class GetTokenizerInfoHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<GetTokenizerInfoHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTokenizerInfoHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="providerFactory">The factory for creating inference provider instances.</param>
    /// <param name="logger">The logger instance.</param>
    public GetTokenizerInfoHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ILogger<GetTokenizerInfoHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves tokenizer information for the specified inference instance.
    /// </summary>
    /// <param name="query">The query containing the instance ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing tokenizer information on success.</returns>
    public async Task<Result<TokenizerInfoDto>> HandleAsync(GetTokenizerInfoQuery query, CancellationToken ct)
    {
        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == query.InstanceId, ct);

        if (instance is null)
        {
            return Error.NotFound($"Inference instance '{query.InstanceId}' was not found.");
        }

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        Result<TokenizerInfo> result = await provider.GetTokenizerInfoAsync(ct);

        if (result.IsFailure)
        {
            _logger.LogWarning(
                "GetTokenizerInfo failed for instance {InstanceId}: {ErrorMessage}",
                query.InstanceId, result.Error.Message);
            return result.Error;
        }

        TokenizerInfo info = result.Value;
        return new TokenizerInfoDto(
            info.VocabSize,
            info.TokenizerType,
            info.SpecialTokens.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            info.ModelId);
    }
}
