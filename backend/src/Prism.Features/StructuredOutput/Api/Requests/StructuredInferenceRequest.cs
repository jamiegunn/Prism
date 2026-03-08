using Prism.Common.Inference.Models;

namespace Prism.Features.StructuredOutput.Api.Requests;

/// <summary>
/// Request body for structured inference with guided decoding.
/// </summary>
public sealed record StructuredInferenceRequest(
    Guid InstanceId,
    string Model,
    List<ChatMessage> Messages,
    double? Temperature,
    int? MaxTokens);
