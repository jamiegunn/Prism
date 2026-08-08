using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Results;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;
using Prism.Features.StructuredOutput.Application.Dtos;
using Prism.Features.StructuredOutput.Domain;

namespace Prism.Features.StructuredOutput.Application.StructuredInference;

/// <summary>
/// Command to execute structured inference with guided JSON decoding.
/// </summary>
public sealed record StructuredInferenceCommand(
    Guid SchemaId,
    Guid InstanceId,
    string Model,
    List<ChatMessage> Messages,
    double? Temperature,
    int? MaxTokens);

/// <summary>
/// Handles structured inference by calling the model with guided JSON decoding and validating the result.
/// </summary>
public sealed class StructuredInferenceHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<StructuredInferenceHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredInferenceHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="providerFactory">The inference provider factory.</param>
    /// <param name="logger">The logger instance.</param>
    public StructuredInferenceHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ILogger<StructuredInferenceHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Executes structured inference with guided decoding.
    /// </summary>
    /// <param name="command">The inference command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the structured inference result.</returns>
    public async Task<Result<StructuredInferenceResultDto>> HandleAsync(StructuredInferenceCommand command, CancellationToken ct)
    {
        JsonSchemaEntity? schema = await _db.Set<JsonSchemaEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == command.SchemaId, ct);

        if (schema is null)
            return Error.NotFound($"JSON schema {command.SchemaId} not found.");

        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == command.InstanceId, ct);

        if (instance is null)
            return Error.NotFound($"Inference instance {command.InstanceId} not found.");

        var sw = Stopwatch.StartNew();

        var chatRequest = new ChatRequest
        {
            Model = command.Model,
            Messages = command.Messages,
            Temperature = command.Temperature ?? 0.1,
            MaxTokens = command.MaxTokens ?? 2048,
            SourceModule = "structured-output"
        };

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        // Only ask for native guidance where the provider actually has it. Sending a constraint
        // to a provider that ignores it produces unconstrained output that looks guided, which
        // is worse than knowing you are on the fallback path.
        bool guided = provider.Capabilities.SupportsGuidedDecoding;

        chatRequest = guided
            ? chatRequest with { JsonSchema = schema.SchemaJson }
            : chatRequest with
            {
                // Fallback: instruct rather than constrain, and let validation catch the misses.
                Messages =
                [
                    ChatMessage.System(
                        "Respond with a single JSON document and nothing else. No prose, no code "
                        + "fences. It must conform to this JSON Schema:\n" + schema.SchemaJson),
                    .. chatRequest.Messages,
                ],
                ResponseFormat = "json_object",
            };

        Result<ChatResponse> chatResult = await provider.ChatAsync(chatRequest, ct);
        sw.Stop();

        if (chatResult.IsFailure)
            return Result<StructuredInferenceResultDto>.Failure(chatResult.Error);

        ChatResponse response = chatResult.Value;
        string rawOutput = response.Content;

        SchemaValidationResult validation = JsonSchemaValidator.Validate(rawOutput, schema.SchemaJson);

        object? parsedJson = null;

        if (validation.SchemaError is null)
        {
            try
            {
                parsedJson = JsonSerializer.Deserialize<object>(rawOutput);
            }
            catch (JsonException)
            {
                // Already reported by the validator; the raw output is still returned so the
                // failure can be inspected.
            }
        }

        bool isValid = validation.IsValid;
        List<string> validationErrors = [.. validation.Errors];

        if (!guided)
        {
            validationErrors.Insert(
                0,
                $"Note: {instance.Name} does not support guided decoding, so the schema was "
                + "requested by instruction rather than enforced during generation.");
        }

        _logger.LogInformation(
            "Structured inference for schema {SchemaName}: valid={IsValid}, guided={Guided}, {LatencyMs}ms",
            schema.Name, isValid, guided, sw.ElapsedMilliseconds);

        return new StructuredInferenceResultDto(
            rawOutput,
            parsedJson,
            isValid,
            validationErrors,
            response.Usage?.PromptTokens ?? 0,
            response.Usage?.CompletionTokens ?? 0,
            sw.Elapsed.TotalMilliseconds);
    }
}
