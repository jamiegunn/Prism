namespace Prism.Common.Storage;

/// <summary>
/// Value object representing a normalized storage path.
/// Enforces forward slashes, prevents path traversal attacks, and validates path structure.
/// </summary>
public sealed record StoragePath
{
    /// <summary>
    /// Gets the normalized path string using forward slashes.
    /// </summary>
    public string Value { get; }

    private StoragePath(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a new <see cref="StoragePath"/> from a raw path string.
    /// Normalizes separators to forward slashes, removes leading slashes, and validates against path traversal.
    /// </summary>
    /// <param name="path">The raw path string to normalize.</param>
    /// <returns>A validated and normalized <see cref="StoragePath"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is null, empty, or contains path traversal sequences.</exception>
    public static StoragePath From(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Storage path cannot be null or empty.", nameof(path));
        }

        string normalized = path
            .Replace('\\', '/')
            .TrimStart('/');

        if (normalized.Contains(".."))
        {
            throw new ArgumentException("Storage path cannot contain path traversal sequences (..).", nameof(path));
        }

        if (normalized.Contains("//"))
        {
            throw new ArgumentException("Storage path cannot contain double slashes.", nameof(path));
        }

        if (normalized.Length == 0)
        {
            throw new ArgumentException("Storage path cannot be empty after normalization.", nameof(path));
        }

        return new StoragePath(normalized);
    }

    /// <summary>
    /// Gets the file name (last segment) of the storage path.
    /// </summary>
    /// <returns>The file name portion of the path.</returns>
    public string GetFileName() =>
        Value.Contains('/') ? Value[(Value.LastIndexOf('/') + 1)..] : Value;

    /// <summary>
    /// Gets the directory portion of the storage path.
    /// </summary>
    /// <returns>The directory portion, or an empty string if the path has no directory.</returns>
    public string GetDirectory() =>
        Value.Contains('/') ? Value[..Value.LastIndexOf('/')] : string.Empty;

    /// <summary>
    /// Gets the file extension including the leading dot.
    /// </summary>
    /// <returns>The file extension, or an empty string if there is no extension.</returns>
    public string GetExtension()
    {
        string fileName = GetFileName();
        int dotIndex = fileName.LastIndexOf('.');
        return dotIndex >= 0 ? fileName[dotIndex..] : string.Empty;
    }

    /// <summary>
    /// Creates a new <see cref="StoragePath"/> by combining this path with a child segment.
    /// </summary>
    /// <param name="child">The child path segment to append.</param>
    /// <returns>A new <see cref="StoragePath"/> representing the combined path.</returns>
    public StoragePath Combine(string child) =>
        From($"{Value}/{child}");

    /// <summary>
    /// Returns the normalized path string.
    /// </summary>
    /// <returns>The normalized path string.</returns>
    public override string ToString() => Value;
}
