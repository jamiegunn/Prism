namespace Prism.Features.FineTuning.Domain;

/// <summary>
/// Fine-tuning dataset export formats.
/// </summary>
public enum FineTuneExportFormat
{
    /// <summary>Alpaca format: instruction/input/output JSON.</summary>
    Alpaca,

    /// <summary>ShareGPT format: conversations array.</summary>
    ShareGpt,

    /// <summary>ChatML format: with role tokens.</summary>
    ChatMl,

    /// <summary>OpenAI JSONL format: messages array.</summary>
    OpenAiJsonl
}
