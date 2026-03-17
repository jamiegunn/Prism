using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Prism.Common.Results;

namespace Prism.Common.Inference.Runtime;

/// <summary>
/// Records inference runs by publishing to a <see cref="Channel{T}"/> for async persistence.
/// This bridges the runtime to the existing <c>InferenceRecordPersistenceService</c> background worker.
/// </summary>
public sealed class ChannelInferenceRecorder : IInferenceRecorder
{
    private readonly Channel<InferenceRecordData> _channel;
    private readonly ILogger<ChannelInferenceRecorder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelInferenceRecorder"/> class.
    /// </summary>
    /// <param name="channel">The channel used to pass records to the persistence background service.</param>
    /// <param name="logger">The logger for recording operations.</param>
    public ChannelInferenceRecorder(Channel<InferenceRecordData> channel, ILogger<ChannelInferenceRecorder> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Result> RecordAsync(InferenceRecordData record, InferenceRunOptions options, CancellationToken ct)
    {
        if (options.SkipRecording)
        {
            _logger.LogDebug("Skipping recording for run {RunId} (SkipRecording=true)", record.Id);
            return Task.FromResult(Result.Success());
        }

        bool written = _channel.Writer.TryWrite(record);

        if (!written)
        {
            _logger.LogWarning("Failed to enqueue inference record {RunId} — channel is full", record.Id);
            return Task.FromResult(Result.Failure(Error.Unavailable("Recording channel is full. Record was not persisted.")));
        }

        _logger.LogDebug("Enqueued inference record {RunId} for persistence", record.Id);
        return Task.FromResult(Result.Success());
    }
}
