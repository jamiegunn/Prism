using Microsoft.EntityFrameworkCore;
using Prism.Common.Inference;
using Prism.Common.Inference.Metrics;
using Prism.Common.Inference.Models;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;
using Prism.Features.TokenExplorer.Application.Dtos;

namespace Prism.Features.TokenExplorer.Application.ExploreBranch;

/// <summary>
/// Handles branch exploration by forcing a starting token and generating a multi-token continuation.
/// Returns token-by-token logprob data and perplexity for the generated branch.
/// </summary>
public sealed class ExploreBranchHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<ExploreBranchHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExploreBranchHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="providerFactory">The factory for creating inference provider instances.</param>
    /// <param name="logger">The logger instance.</param>
    public ExploreBranchHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ILogger<ExploreBranchHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Explores an alternative generation branch by prepending a forced token to an assistant prefill
    /// and generating a multi-token continuation with logprobs.
    /// </summary>
    /// <param name="command">The branch exploration command with prompt, forced token, and generation parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the branch exploration data on success, or an error on failure.</returns>
    public async Task<Result<BranchExplorationDto>> HandleAsync(ExploreBranchCommand command, CancellationToken ct)
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

        // Use the prompt as user message and the forced token as the beginning of the assistant response.
        // This causes the model to continue generation from the forced token.
        var chatRequest = new ChatRequest
        {
            Model = instance.ModelId ?? "",
            Messages =
            [
                ChatMessage.User(command.Prompt),
                ChatMessage.Assistant(command.ForcedToken)
            ],
            MaxTokens = command.MaxTokens,
            Logprobs = true,
            TopLogprobs = command.TopLogprobs,
            Temperature = command.Temperature ?? 0,
            EnableThinking = command.EnableThinking,
            Stream = false,
            SourceModule = "token-explorer"
        };

        Result<ChatResponse> chatResult = await provider.ChatAsync(chatRequest, ct);

        if (chatResult.IsFailure)
        {
            _logger.LogWarning(
                "Branch exploration failed for instance {InstanceId}: {ErrorMessage}",
                command.InstanceId, chatResult.Error.Message);
            return chatResult.Error;
        }

        ChatResponse response = chatResult.Value;
        string generatedText = command.ForcedToken + response.Content;
        string modelId = instance.ModelId ?? "";

        // Build token-by-token data from logprobs
        var tokens = new List<BranchTokenDto>();

        if (response.LogprobsData is not null)
        {
            foreach (TokenLogprob tokenLogprob in response.LogprobsData.Tokens)
            {
                List<TokenAlternativeDto> alternatives = tokenLogprob.TopLogprobs
                    .Select(tl => new TokenAlternativeDto(tl.Token, tl.Logprob, tl.Probability))
                    .ToList();

                tokens.Add(new BranchTokenDto(
                    tokenLogprob.Token,
                    tokenLogprob.Logprob,
                    tokenLogprob.Probability,
                    alternatives));
            }
        }

        // Calculate perplexity if logprobs are available
        double? perplexity = response.LogprobsData is not null && response.LogprobsData.Tokens.Count > 0
            ? LogprobsCalculator.CalculatePerplexity(response.LogprobsData)
            : null;

        _logger.LogInformation(
            "Branch exploration completed for instance {InstanceId}: {TokenCount} tokens, perplexity {Perplexity}",
            command.InstanceId, tokens.Count, perplexity);

        return new BranchExplorationDto(
            command.ForcedToken,
            generatedText,
            tokens,
            perplexity,
            modelId);
    }
}
