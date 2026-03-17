using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Results;

namespace Prism.Features.Evaluation.Domain.Scorers;

/// <summary>
/// Uses an LLM as a judge to score the quality of a generated response.
/// Sends the input, expected output, and actual output to a model with a rubric prompt,
/// then parses a numeric score from the response.
/// </summary>
public sealed class LlmJudgeScorer : IScoringMethod
{
    private readonly IInferenceProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmJudgeScorer"/> class.
    /// </summary>
    /// <param name="provider">The inference provider to use for judging.</param>
    public LlmJudgeScorer(IInferenceProvider provider)
    {
        _provider = provider;
    }

    /// <inheritdoc />
    public string Name => "llm_judge";

    /// <inheritdoc />
    public async Task<double> ScoreAsync(string input, string expected, string actual, CancellationToken ct)
    {
        string systemPrompt = """
            You are an evaluation judge. Score the quality of the actual output compared to the expected output.
            Consider accuracy, completeness, and relevance.
            Respond with ONLY a number between 0.0 and 1.0, where 1.0 is a perfect match.
            Do not include any explanation, just the number.
            """;

        string userMessage = $"""
            Input: {input}

            Expected Output: {expected}

            Actual Output: {actual}

            Score (0.0 to 1.0):
            """;

        var request = new ChatRequest
        {
            Model = "",
            Messages =
            [
                ChatMessage.System(systemPrompt),
                ChatMessage.User(userMessage)
            ],
            Temperature = 0.0,
            MaxTokens = 10,
            Stream = false,
            SourceModule = "evaluation-judge"
        };

        Result<ChatResponse> result = await _provider.ChatAsync(request, ct);

        if (result.IsFailure)
        {
            return 0.0;
        }

        string content = result.Value.Content.Trim();

        if (double.TryParse(content, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double score))
        {
            return Math.Clamp(score, 0.0, 1.0);
        }

        return 0.0;
    }
}
