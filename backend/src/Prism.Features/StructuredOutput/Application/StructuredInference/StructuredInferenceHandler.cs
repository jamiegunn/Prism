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
            ResponseFormat = schema.SchemaJson,
            SourceModule = "structured-output"
        };

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        Result<ChatResponse> chatResult = await provider.ChatAsync(chatRequest, ct);
        sw.Stop();

        if (chatResult.IsFailure)
            return Result<StructuredInferenceResultDto>.Failure(chatResult.Error);

        ChatResponse response = chatResult.Value;
        string rawOutput = response.Content;

        // Attempt to parse JSON
        object? parsedJson = null;
        bool isValid = false;
        var validationErrors = new List<string>();

        try
        {
            JsonDocument doc = JsonDocument.Parse(rawOutput);
            parsedJson = JsonSerializer.Deserialize<object>(rawOutput);
            isValid = true;

            validationErrors = ValidateAgainstSchema(doc, schema.SchemaJson);
            if (validationErrors.Count > 0)
                isValid = false;
        }
        catch (JsonException ex)
        {
            validationErrors.Add($"Invalid JSON: {ex.Message}");
        }

        _logger.LogInformation("Structured inference completed for schema {SchemaName}: valid={IsValid}, {LatencyMs}ms",
            schema.Name, isValid, sw.ElapsedMilliseconds);

        return new StructuredInferenceResultDto(
            rawOutput,
            parsedJson,
            isValid,
            validationErrors,
            response.Usage?.PromptTokens ?? 0,
            response.Usage?.CompletionTokens ?? 0,
            sw.Elapsed.TotalMilliseconds);
    }

    private static List<string> ValidateAgainstSchema(JsonDocument doc, string schemaJson)
    {
        var errors = new List<string>();

        try
        {
            JsonDocument schema = JsonDocument.Parse(schemaJson);

            if (schema.RootElement.TryGetProperty("required", out JsonElement required) &&
                required.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement requiredField in required.EnumerateArray())
                {
                    string fieldName = requiredField.GetString() ?? "";
                    if (!doc.RootElement.TryGetProperty(fieldName, out _))
                    {
                        errors.Add($"Missing required field: {fieldName}");
                    }
                }
            }

            if (schema.RootElement.TryGetProperty("properties", out JsonElement properties) &&
                properties.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty prop in properties.EnumerateObject())
                {
                    if (doc.RootElement.TryGetProperty(prop.Name, out JsonElement value) &&
                        prop.Value.TryGetProperty("type", out JsonElement expectedType))
                    {
                        string expected = expectedType.GetString() ?? "";
                        bool typeMatch = expected switch
                        {
                            "string" => value.ValueKind == JsonValueKind.String,
                            "number" => value.ValueKind == JsonValueKind.Number,
                            "integer" => value.ValueKind == JsonValueKind.Number,
                            "boolean" => value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False,
                            "array" => value.ValueKind == JsonValueKind.Array,
                            "object" => value.ValueKind == JsonValueKind.Object,
                            "null" => value.ValueKind == JsonValueKind.Null,
                            _ => true
                        };

                        if (!typeMatch)
                        {
                            errors.Add($"Field '{prop.Name}' expected type '{expected}' but got '{value.ValueKind}'");
                        }
                    }
                }
            }
        }
        catch
        {
            // Schema parsing failed — skip validation
        }

        return errors;
    }
}
