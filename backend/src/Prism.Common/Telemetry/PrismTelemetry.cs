using System.Diagnostics;

namespace Prism.Common.Telemetry;

/// <summary>
/// Prism's telemetry root: the single <see cref="ActivitySource"/> inference spans come
/// from, and the switch controlling whether prompt/completion content rides on them.
/// </summary>
public static class PrismTelemetry
{
    /// <summary>
    /// The activity source name — what a collector subscribes to and what
    /// <c>AddSource(...)</c> registers.
    /// </summary>
    public const string InferenceSourceName = "Prism.Inference";

    /// <summary>
    /// The source every inference span is started from.
    /// </summary>
    public static readonly ActivitySource InferenceSource = new(InferenceSourceName);

    /// <summary>
    /// Gets or sets whether prompt and completion content are attached to spans. Default
    /// false and deliberately so: content is sensitive, the GenAI semantic conventions make
    /// it opt-in, and a trace pipeline is usually shipped somewhere logs are not. Set from
    /// configuration key <c>Prism:Telemetry:CaptureContent</c> at startup.
    /// </summary>
    public static bool CaptureContent { get; set; }
}
