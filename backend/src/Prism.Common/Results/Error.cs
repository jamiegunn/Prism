namespace Prism.Common.Results;

/// <summary>
/// Represents a domain error with a code, message, and type classification.
/// </summary>
/// <param name="Code">A machine-readable error code.</param>
/// <param name="Message">A human-readable error message.</param>
/// <param name="Type">The classification of the error.</param>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>
    /// Creates a not-found error.
    /// </summary>
    /// <param name="message">The error message describing what was not found.</param>
    /// <returns>An <see cref="Error"/> with <see cref="ErrorType.NotFound"/> type.</returns>
    public static Error NotFound(string message) => new("NotFound", message, ErrorType.NotFound);

    /// <summary>
    /// Creates a validation error.
    /// </summary>
    /// <param name="message">The error message describing the validation failure.</param>
    /// <returns>An <see cref="Error"/> with <see cref="ErrorType.Validation"/> type.</returns>
    public static Error Validation(string message) => new("Validation", message, ErrorType.Validation);

    /// <summary>
    /// Creates a conflict error.
    /// </summary>
    /// <param name="message">The error message describing the conflict.</param>
    /// <returns>An <see cref="Error"/> with <see cref="ErrorType.Conflict"/> type.</returns>
    public static Error Conflict(string message) => new("Conflict", message, ErrorType.Conflict);

    /// <summary>
    /// Creates an internal server error.
    /// </summary>
    /// <param name="message">The error message describing the internal failure.</param>
    /// <returns>An <see cref="Error"/> with <see cref="ErrorType.Internal"/> type.</returns>
    public static Error Internal(string message) => new("Internal", message, ErrorType.Internal);

    /// <summary>
    /// Creates a service unavailable error.
    /// </summary>
    /// <param name="message">The error message describing the unavailability.</param>
    /// <returns>An <see cref="Error"/> with <see cref="ErrorType.Unavailable"/> type.</returns>
    public static Error Unavailable(string message) => new("Unavailable", message, ErrorType.Unavailable);
}

/// <summary>
/// Classifies the type of error for appropriate HTTP status code mapping.
/// </summary>
public enum ErrorType
{
    /// <summary>Input validation failure (400).</summary>
    Validation,

    /// <summary>Requested resource was not found (404).</summary>
    NotFound,

    /// <summary>Resource state conflict (409).</summary>
    Conflict,

    /// <summary>Unexpected internal error (500).</summary>
    Internal,

    /// <summary>Service or dependency unavailable (503).</summary>
    Unavailable
}
