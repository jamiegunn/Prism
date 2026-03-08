using Microsoft.EntityFrameworkCore;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;
using Prism.Features.TokenExplorer.Application.Dtos;

namespace Prism.Features.TokenExplorer.Application.PredictNextToken;

/// <summary>
/// Handles next-token prediction by sending a single-token inference request with logprobs enabled.
/// Extracts the top-K token predictions and builds cumulative probabilities for top-p visualization.
/// </summary>
public sealed class PredictNextTokenHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<PredictNextTokenHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PredictNextTokenHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="providerFactory">The factory for creating inference provider instances.</param>
    /// <param name="logger">The logger instance.</param>
    public PredictNextTokenHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ILogger<PredictNextTokenHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Predicts the next token for the given prompt by requesting a single-token completion with logprobs.
    /// </summary>
    /// <param name="command">The prediction command containing the prompt and parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the next-token predictions on success, or an error on failure.</returns>
    public async Task<Result<NextTokenPredictionDto>> HandleAsync(PredictNextTokenCommand command, CancellationToken ct)
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

        var messages = new List<ChatMessage> { ChatMessage.User(command.Prompt) };
        if (!string.IsNullOrEmpty(command.AssistantPrefix))
        {
            messages.Add(ChatMessage.Assistant(command.AssistantPrefix));
        }

        var chatRequest = new ChatRequest
        {
            Model = instance.ModelId ?? "",
            Messages = messages,
            MaxTokens = 1,
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
                "Next-token prediction failed for instance {InstanceId}: {ErrorMessage}",
                command.InstanceId, chatResult.Error.Message);
            return chatResult.Error;
        }

        ChatResponse response = chatResult.Value;

        return BuildPredictionDto(response, instance.ModelId ?? "");
    }

    /// <summary>
    /// Builds a <see cref="NextTokenPredictionDto"/> from the chat response logprobs data.
    /// </summary>
    /// <param name="response">The chat completion response containing logprobs.</param>
    /// <param name="modelId">The model identifier for the response.</param>
    /// <returns>A result containing the prediction DTO or a validation error if no logprobs are available.</returns>
    internal static Result<NextTokenPredictionDto> BuildPredictionDto(ChatResponse response, string modelId)
    {
        if (response.LogprobsData is null || response.LogprobsData.Tokens.Count == 0)
        {
            return Error.Validation("The inference provider did not return logprobs data. Ensure the model supports logprobs.");
        }

        TokenLogprob firstToken = response.LogprobsData.Tokens[0];
        List<TopLogprob> topLogprobs = firstToken.TopLogprobs;

        double cumulativeProbability = 0;
        var predictions = new List<TokenPredictionEntry>(topLogprobs.Count);

        foreach (TopLogprob topLogprob in topLogprobs)
        {
            double probability = topLogprob.Probability;
            cumulativeProbability += probability;

            predictions.Add(new TokenPredictionEntry(
                topLogprob.Token,
                topLogprob.Logprob,
                probability,
                cumulativeProbability));
        }

        double totalProbability = predictions.Count > 0
            ? predictions[^1].CumulativeProbability
            : 0;

        int inputTokenCount = response.Usage?.PromptTokens ?? 0;

        return new NextTokenPredictionDto(predictions, inputTokenCount, modelId, totalProbability);
    }
}
