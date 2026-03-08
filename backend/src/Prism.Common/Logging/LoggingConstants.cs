namespace Prism.Common.Logging;

/// <summary>
/// Defines constant property names used in structured logging throughout the application.
/// Use these constants instead of magic strings to ensure consistency across log entries.
/// </summary>
public static class LoggingConstants
{
    /// <summary>The correlation ID that ties together all log entries for a single request.</summary>
    public const string CorrelationId = "CorrelationId";

    /// <summary>The authenticated user's identifier.</summary>
    public const string UserId = "UserId";

    /// <summary>The feature or module that produced the log entry.</summary>
    public const string Feature = "Feature";

    /// <summary>The name of the inference provider being used.</summary>
    public const string ProviderName = "ProviderName";

    /// <summary>The inference provider endpoint URL.</summary>
    public const string ProviderEndpoint = "ProviderEndpoint";

    /// <summary>The model identifier being used for inference.</summary>
    public const string ModelId = "ModelId";

    /// <summary>The duration of an operation in milliseconds.</summary>
    public const string DurationMs = "DurationMs";

    /// <summary>The HTTP method of the request.</summary>
    public const string HttpMethod = "HttpMethod";

    /// <summary>The HTTP request path.</summary>
    public const string RequestPath = "RequestPath";

    /// <summary>The HTTP response status code.</summary>
    public const string StatusCode = "StatusCode";

    /// <summary>The number of tokens in the prompt.</summary>
    public const string PromptTokens = "PromptTokens";

    /// <summary>The number of tokens in the completion.</summary>
    public const string CompletionTokens = "CompletionTokens";

    /// <summary>The entity type being operated on.</summary>
    public const string EntityType = "EntityType";

    /// <summary>The entity identifier being operated on.</summary>
    public const string EntityId = "EntityId";

    /// <summary>The name of the operation being performed.</summary>
    public const string Operation = "Operation";

    /// <summary>The name of the job being executed.</summary>
    public const string JobName = "JobName";

    /// <summary>The unique identifier of a job execution.</summary>
    public const string JobId = "JobId";

    /// <summary>The name of the cache region.</summary>
    public const string CacheRegion = "CacheRegion";

    /// <summary>The cache key being accessed.</summary>
    public const string CacheKey = "CacheKey";

    /// <summary>Whether the cache operation was a hit or miss.</summary>
    public const string CacheHit = "CacheHit";

    /// <summary>The export format being used.</summary>
    public const string ExportFormat = "ExportFormat";

    /// <summary>The source module that initiated an inference call.</summary>
    public const string SourceModule = "SourceModule";
}
