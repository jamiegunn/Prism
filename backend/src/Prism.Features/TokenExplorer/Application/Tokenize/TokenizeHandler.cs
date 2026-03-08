using System.Text;
using Microsoft.EntityFrameworkCore;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;
using Prism.Features.TokenExplorer.Application.Dtos;

namespace Prism.Features.TokenExplorer.Application.Tokenize;

/// <summary>
/// Handles tokenization of text using a specific inference instance.
/// Looks up the instance, creates the provider, and builds a detailed tokenization result
/// with display text, hex bytes, and aggregate statistics.
/// </summary>
public sealed class TokenizeHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<TokenizeHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenizeHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="providerFactory">The factory for creating inference provider instances.</param>
    /// <param name="logger">The logger instance.</param>
    public TokenizeHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ILogger<TokenizeHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Tokenizes the given text using the specified inference instance.
    /// </summary>
    /// <param name="command">The tokenize command containing the instance ID and text.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the tokenization details on success, or an error on failure.</returns>
    public async Task<Result<TokenizeResultDto>> HandleAsync(TokenizeCommand command, CancellationToken ct)
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

        Result<TokenizeResponse> tokenizeResult = await provider.TokenizeAsync(command.Text, ct);

        if (tokenizeResult.IsFailure)
        {
            _logger.LogWarning(
                "Tokenization failed for instance {InstanceId}: {ErrorMessage}",
                command.InstanceId, tokenizeResult.Error.Message);
            return tokenizeResult.Error;
        }

        TokenizeResponse response = tokenizeResult.Value;
        TokenizeResultDto dto = BuildTokenizeResultDto(response, command.Text, instance.ModelId ?? "");

        return dto;
    }

    /// <summary>
    /// Builds a <see cref="TokenizeResultDto"/> from a tokenize response, enriching each token
    /// with display text and hex byte representations.
    /// </summary>
    /// <param name="response">The raw tokenize response from the provider.</param>
    /// <param name="text">The original input text.</param>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>The enriched tokenization result DTO.</returns>
    internal static TokenizeResultDto BuildTokenizeResultDto(TokenizeResponse response, string text, string modelId)
    {
        var tokenBlocks = new List<TokenBlockDto>(response.Tokens.Count);

        foreach (TokenInfo token in response.Tokens)
        {
            string displayText = BuildDisplayText(token.Text);
            string hexBytes = BuildHexBytes(token.Text);

            tokenBlocks.Add(new TokenBlockDto(
                token.Id,
                token.Text,
                displayText,
                token.ByteLength,
                hexBytes));
        }

        int characterCount = text.Length;
        int byteCount = Encoding.UTF8.GetByteCount(text);

        return new TokenizeResultDto(tokenBlocks, response.TokenCount, characterCount, byteCount, modelId);
    }

    /// <summary>
    /// Builds a display-friendly text representation by replacing whitespace characters
    /// with visible alternatives: spaces become middle dots, newlines become return arrows,
    /// and tabs become right arrows.
    /// </summary>
    /// <param name="text">The raw token text.</param>
    /// <returns>The text with visible whitespace characters.</returns>
    internal static string BuildDisplayText(string text)
    {
        return text
            .Replace(" ", "\u00B7")
            .Replace("\n", "\u21B5\n")
            .Replace("\t", "\u2192\t");
    }

    /// <summary>
    /// Builds a hex byte string from the UTF-8 encoding of the given text,
    /// formatted as space-separated uppercase hex pairs (e.g., "48 65 6C 6C 6F").
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <returns>The hex byte representation.</returns>
    internal static string BuildHexBytes(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        return string.Join(" ", bytes.Select(b => b.ToString("X2")));
    }
}
