using Prism.Features.TokenExplorer.Application.Dtos;
using Prism.Features.TokenExplorer.Application.PredictNextToken;

namespace Prism.Features.TokenExplorer.Application.StepThrough;

/// <summary>
/// Handles step-through token generation by appending a selected token to the prompt
/// and predicting the next token from the extended position.
/// </summary>
public sealed class StepThroughHandler
{
    private readonly PredictNextTokenHandler _predictHandler;
    private readonly ILogger<StepThroughHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StepThroughHandler"/> class.
    /// </summary>
    /// <param name="predictHandler">The predict next token handler for generating predictions.</param>
    /// <param name="logger">The logger instance.</param>
    public StepThroughHandler(
        PredictNextTokenHandler predictHandler,
        ILogger<StepThroughHandler> logger)
    {
        _predictHandler = predictHandler;
        _logger = logger;
    }

    /// <summary>
    /// Appends the selected token to the prompt and predicts the next token at the extended position.
    /// </summary>
    /// <param name="command">The step-through command containing the prompt, selected token, and parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the step-through result on success, or an error on failure.</returns>
    public async Task<Result<StepThroughResultDto>> HandleAsync(StepThroughCommand command, CancellationToken ct)
    {
        string assistantPrefix = command.PreviousTokens + command.SelectedToken;
        string extendedText = command.Prompt + assistantPrefix;

        _logger.LogDebug(
            "Stepping through with token {SelectedToken}, assistant prefix length: {PrefixLength}",
            command.SelectedToken, assistantPrefix.Length);

        var predictCommand = new PredictNextTokenCommand(
            command.InstanceId,
            command.Prompt,
            command.TopLogprobs,
            command.Temperature,
            command.EnableThinking,
            assistantPrefix);

        Result<NextTokenPredictionDto> predictionResult = await _predictHandler.HandleAsync(predictCommand, ct);

        if (predictionResult.IsFailure)
        {
            return Result<StepThroughResultDto>.Failure(predictionResult.Error);
        }

        return new StepThroughResultDto(
            extendedText,
            command.SelectedToken,
            predictionResult.Value);
    }
}
