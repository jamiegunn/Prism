namespace Prism.Common.Storage;

/// <summary>
/// Represents metadata about a stored file.
/// </summary>
/// <param name="Path">The storage path of the file.</param>
/// <param name="SizeBytes">The size of the file in bytes.</param>
/// <param name="ContentType">The MIME content type of the file.</param>
/// <param name="CreatedAt">The UTC timestamp when the file was created.</param>
/// <param name="ModifiedAt">The UTC timestamp when the file was last modified.</param>
/// <param name="ETag">The optional entity tag for concurrency control.</param>
public sealed record FileMetadata(
    StoragePath Path,
    long SizeBytes,
    string ContentType,
    DateTime CreatedAt,
    DateTime ModifiedAt,
    string? ETag);
