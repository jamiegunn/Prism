using Prism.Common.Results;

namespace Prism.Common.Storage.Providers;

/// <summary>
/// A no-op implementation of <see cref="IFileStorage"/> for testing scenarios.
/// All operations succeed but no files are actually stored.
/// </summary>
public sealed class NullFileStorage : IFileStorage
{
    /// <summary>
    /// Returns a successful result with synthetic metadata without storing the file.
    /// </summary>
    /// <param name="path">The storage path (used for metadata only).</param>
    /// <param name="content">The file content stream (not read).</param>
    /// <param name="contentType">The MIME content type.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A successful result with synthetic file metadata.</returns>
    public Task<Result<FileMetadata>> StoreAsync(StoragePath path, Stream content, string contentType, CancellationToken ct)
    {
        FileMetadata metadata = new(
            Path: path,
            SizeBytes: content.CanSeek ? content.Length : 0,
            ContentType: contentType,
            CreatedAt: DateTime.UtcNow,
            ModifiedAt: DateTime.UtcNow,
            ETag: null);

        return Task.FromResult<Result<FileMetadata>>(metadata);
    }

    /// <summary>
    /// Returns an empty stream.
    /// </summary>
    /// <param name="path">The storage path (ignored).</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A successful result with an empty memory stream.</returns>
    public Task<Result<Stream>> RetrieveAsync(StoragePath path, CancellationToken ct) =>
        Task.FromResult<Result<Stream>>(new MemoryStream() as Stream);

    /// <summary>
    /// Returns a successful result without deleting anything.
    /// </summary>
    /// <param name="path">The storage path (ignored).</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A successful result.</returns>
    public Task<Result> DeleteAsync(StoragePath path, CancellationToken ct) =>
        Task.FromResult(Result.Success());

    /// <summary>
    /// Always returns false (no files exist).
    /// </summary>
    /// <param name="path">The storage path (ignored).</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A successful result containing false.</returns>
    public Task<Result<bool>> ExistsAsync(StoragePath path, CancellationToken ct) =>
        Task.FromResult<Result<bool>>(false);

    /// <summary>
    /// Returns a not-found error since no files are stored.
    /// </summary>
    /// <param name="path">The storage path.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A not-found error result.</returns>
    public Task<Result<FileMetadata>> GetMetadataAsync(StoragePath path, CancellationToken ct) =>
        Task.FromResult<Result<FileMetadata>>(Error.NotFound($"File not found: {path.Value}"));

    /// <summary>
    /// Returns an empty list since no files are stored.
    /// </summary>
    /// <param name="directoryPath">The directory path (ignored).</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A successful result with an empty list.</returns>
    public Task<Result<IReadOnlyList<FileMetadata>>> ListAsync(StoragePath directoryPath, CancellationToken ct) =>
        Task.FromResult<Result<IReadOnlyList<FileMetadata>>>(Array.Empty<FileMetadata>());

    /// <summary>
    /// Returns a synthetic download URL.
    /// </summary>
    /// <param name="path">The storage path.</param>
    /// <param name="expiresIn">Not used.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A successful result with a synthetic URL.</returns>
    public Task<Result<string>> GetDownloadUrlAsync(StoragePath path, TimeSpan? expiresIn = null, CancellationToken ct = default) =>
        Task.FromResult<Result<string>>($"/null-storage/{path.Value}");
}
