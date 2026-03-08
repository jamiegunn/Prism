using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Prism.Features.TokenExplorer.Api.Requests;
using Prism.Features.TokenExplorer.Application.Dtos;
using Prism.Features.TokenExplorer.Application.ExploreBranch;
using Prism.Features.TokenExplorer.Application.PredictNextToken;
using Prism.Features.TokenExplorer.Application.StepThrough;
using Prism.Features.TokenExplorer.Application.Tokenize;

namespace Prism.Features.TokenExplorer.Api;

/// <summary>
/// Defines the HTTP endpoints for the Token Explorer feature including next-token prediction,
/// step-through generation, and branch exploration.
/// </summary>
public static class TokenExplorerEndpoints
{
    /// <summary>
    /// Maps the token explorer endpoints under <c>/api/v1/token-explorer</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder (typically the WebApplication).</param>
    /// <returns>The route group builder for further configuration.</returns>
    public static RouteGroupBuilder MapTokenExplorerEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/token-explorer")
            .WithTags("Token Explorer");

        group.MapPost("/predict", PredictNextToken)
            .WithName("PredictNextToken")
            .WithSummary("Predicts the next token with top-K logprob alternatives")
            .Produces<NextTokenPredictionDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/step", StepThrough)
            .WithName("StepThroughToken")
            .WithSummary("Appends a selected token and predicts the next one")
            .Produces<StepThroughResultDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/branch", ExploreBranch)
            .WithName("ExploreBranch")
            .WithSummary("Explores an alternative generation branch from a forced starting token")
            .Produces<BranchExplorationDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/tokenize", Tokenize)
            .WithName("TokenizeText")
            .WithSummary("Tokenizes text using a specific inference instance and returns per-token details")
            .Produces<TokenizeResultDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/tokenize/compare", CompareTokenize)
            .WithName("CompareTokenize")
            .WithSummary("Tokenizes the same text across multiple inference instances for comparison")
            .Produces<CompareTokenizeResultDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/detokenize", Detokenize)
            .WithName("DetokenizeTokens")
            .WithSummary("Decodes token IDs back to text using a specific inference instance")
            .Produces<DetokenizeResultDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> PredictNextToken(
        [FromBody] PredictNextTokenRequest request,
        PredictNextTokenHandler handler,
        CancellationToken ct)
    {
        var command = new PredictNextTokenCommand(
            request.InstanceId,
            request.Prompt,
            request.TopLogprobs ?? 20,
            request.Temperature,
            request.EnableThinking ?? false);

        Result<NextTokenPredictionDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> StepThrough(
        [FromBody] StepThroughRequest request,
        StepThroughHandler handler,
        CancellationToken ct)
    {
        var command = new StepThroughCommand(
            request.InstanceId,
            request.Prompt,
            request.SelectedToken,
            request.PreviousTokens ?? "",
            request.TopLogprobs ?? 20,
            request.Temperature,
            request.EnableThinking ?? false);

        Result<StepThroughResultDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ExploreBranch(
        [FromBody] ExploreBranchRequest request,
        ExploreBranchHandler handler,
        CancellationToken ct)
    {
        var command = new ExploreBranchCommand(
            request.InstanceId,
            request.Prompt,
            request.ForcedToken,
            request.MaxTokens ?? 50,
            request.Temperature,
            request.TopLogprobs ?? 5,
            request.EnableThinking ?? false);

        Result<BranchExplorationDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> Tokenize(
        [FromBody] TokenizeRequest request,
        TokenizeHandler handler,
        CancellationToken ct)
    {
        var command = new TokenizeCommand(request.InstanceId, request.Text);

        Result<TokenizeResultDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> CompareTokenize(
        [FromBody] CompareTokenizeRequest request,
        CompareTokenizeHandler handler,
        CancellationToken ct)
    {
        var command = new CompareTokenizeCommand(request.InstanceIds, request.Text);

        Result<CompareTokenizeResultDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> Detokenize(
        [FromBody] DetokenizeRequest request,
        DetokenizeHandler handler,
        CancellationToken ct)
    {
        var command = new DetokenizeCommand(request.InstanceId, request.TokenIds);

        Result<DetokenizeResultDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }
}
