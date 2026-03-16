namespace Prism.Features.FineTuning.Api.Requests;

/// <summary>
/// Request to export a dataset in a fine-tuning format.
/// </summary>
public sealed record ExportFineTuneRequest(
    Guid DatasetId,
    string Format,
    string? InstructionColumn,
    string? InputColumn,
    string? OutputColumn);
