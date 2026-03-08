using Prism.Common.Results;

namespace Prism.Common.Storage;

/// <summary>
/// Defines the file storage contract for the application.
/// Implementations provide different storage backends (local filesystem, Azure Blob, S3).
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Stores a file at the specified path.
    /// </summary>
    /// <param name="path">The storage path where the file should be stored.</param>
    /// <param name="content">The file content stream.</param>
    /// <param name="contentType">The MIME content type of the file.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the file metadata on success.</returns>
    Task<Result<FileMetadata>> StoreAsync(StoragePath path, Stream content, string contentType, CancellationToken ct);

    /// <summary>
    /// Retrieves a file's content stream from the specified path.
    /// </summary>
    /// <param name="path">The storage path of the file to retrieve.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the file content stream on success.</returns>
    Task<Result<Stream>> RetrieveAsync(StoragePath path, CancellationToken ct);

    /// <summary>
    /// Deletes a file at the specified path.
    /// </summary>
    /// <param name="path">The storage path of the file to delete.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> DeleteAsync(StoragePath path, CancellationToken ct);

    /// <summary>
    /// Checks whether a file exists at the specified path.
    /// </summary>
    /// <param name="path">The storage path to check.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing true if the file exists; otherwise, false.</returns>
    Task<Result<bool>> ExistsAsync(StoragePath path, CancellationToken ct);

    /// <summary>
    /// Retrieves metadata about a file at the specified path.
    /// </summary>
    /// <param name="path">The storage path of the file.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the file metadata on success.</returns>
    Task<Result<FileMetadata>> GetMetadataAsync(StoragePath path, CancellationToken ct);

    /// <summary>
    /// Lists all files within the specified directory path.
    /// </summary>
    /// <param name="directoryPath">The directory path to list files from.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the list of file metadata.</returns>
    Task<Result<IReadOnlyList<FileMetadata>>> ListAsync(StoragePath directoryPath, CancellationToken ct);

    /// <summary>
    /// Generates a download URL for the file at the specified path.
    /// For local storage, this returns a relative path. For cloud storage, this returns a pre-signed URL.
    /// </summary>
    /// <param name="path">The storage path of the file.</param>
    /// <param name="expiresIn">The optional duration for which the URL is valid.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the download URL string.</returns>
    Task<Result<string>> GetDownloadUrlAsync(StoragePath path, TimeSpan? expiresIn = null, CancellationToken ct = default);
}
