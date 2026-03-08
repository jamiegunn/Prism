using Microsoft.AspNetCore.StaticFiles;
using Prism.Common.Results;

namespace Prism.Common.Storage.Providers;

/// <summary>
/// Local filesystem implementation of <see cref="IFileStorage"/>.
/// Stores files in a configurable base directory on the local filesystem.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorage> _logger;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalFileStorage"/> class.
    /// </summary>
    /// <param name="basePath">The base directory path for file storage.</param>
    /// <param name="logger">The logger instance.</param>
    public LocalFileStorage(string basePath, ILogger<LocalFileStorage> logger)
    {
        _basePath = basePath;
        _logger = logger;
        _contentTypeProvider = new FileExtensionContentTypeProvider();

        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
            _logger.LogInformation("Created storage directory at {BasePath}", _basePath);
        }
    }

    /// <summary>
    /// Stores a file on the local filesystem at the specified path.
    /// </summary>
    /// <param name="path">The storage path where the file should be stored.</param>
    /// <param name="content">The file content stream.</param>
    /// <param name="contentType">The MIME content type of the file.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the file metadata on success.</returns>
    public async Task<Result<FileMetadata>> StoreAsync(StoragePath path, Stream content, string contentType, CancellationToken ct)
    {
        try
        {
            string fullPath = GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);

            if (directory is not null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using FileStream fileStream = new(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await content.CopyToAsync(fileStream, ct);

            FileInfo fileInfo = new(fullPath);
            FileMetadata metadata = new(
                Path: path,
                SizeBytes: fileInfo.Length,
                ContentType: contentType,
                CreatedAt: fileInfo.CreationTimeUtc,
                ModifiedAt: fileInfo.LastWriteTimeUtc,
                ETag: null);

            _logger.LogInformation("Stored file at {StoragePath} ({SizeBytes} bytes)", path.Value, metadata.SizeBytes);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store file at {StoragePath}", path.Value);
            return Error.Internal($"Failed to store file: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves a file's content stream from the local filesystem.
    /// </summary>
    /// <param name="path">The storage path of the file to retrieve.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the file content stream on success.</returns>
    public Task<Result<Stream>> RetrieveAsync(StoragePath path, CancellationToken ct)
    {
        string fullPath = GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Result<Stream>>(Error.NotFound($"File not found: {path.Value}"));
        }

        try
        {
            Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult<Result<Stream>>(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve file at {StoragePath}", path.Value);
            return Task.FromResult<Result<Stream>>(Error.Internal($"Failed to retrieve file: {ex.Message}"));
        }
    }

    /// <summary>
    /// Deletes a file from the local filesystem.
    /// </summary>
    /// <param name="path">The storage path of the file to delete.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Task<Result> DeleteAsync(StoragePath path, CancellationToken ct)
    {
        string fullPath = GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Result>(Error.NotFound($"File not found: {path.Value}"));
        }

        try
        {
            File.Delete(fullPath);
            _logger.LogInformation("Deleted file at {StoragePath}", path.Value);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file at {StoragePath}", path.Value);
            return Task.FromResult<Result>(Error.Internal($"Failed to delete file: {ex.Message}"));
        }
    }

    /// <summary>
    /// Checks whether a file exists on the local filesystem.
    /// </summary>
    /// <param name="path">The storage path to check.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing true if the file exists; otherwise, false.</returns>
    public Task<Result<bool>> ExistsAsync(StoragePath path, CancellationToken ct)
    {
        string fullPath = GetFullPath(path);
        bool exists = File.Exists(fullPath);
        return Task.FromResult<Result<bool>>(exists);
    }

    /// <summary>
    /// Retrieves metadata about a file on the local filesystem.
    /// </summary>
    /// <param name="path">The storage path of the file.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the file metadata on success.</returns>
    public Task<Result<FileMetadata>> GetMetadataAsync(StoragePath path, CancellationToken ct)
    {
        string fullPath = GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Result<FileMetadata>>(Error.NotFound($"File not found: {path.Value}"));
        }

        FileInfo fileInfo = new(fullPath);
        string contentType = ResolveContentType(path.GetFileName());

        FileMetadata metadata = new(
            Path: path,
            SizeBytes: fileInfo.Length,
            ContentType: contentType,
            CreatedAt: fileInfo.CreationTimeUtc,
            ModifiedAt: fileInfo.LastWriteTimeUtc,
            ETag: null);

        return Task.FromResult<Result<FileMetadata>>(metadata);
    }

    /// <summary>
    /// Lists all files within the specified directory on the local filesystem.
    /// </summary>
    /// <param name="directoryPath">The directory path to list files from.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the list of file metadata.</returns>
    public Task<Result<IReadOnlyList<FileMetadata>>> ListAsync(StoragePath directoryPath, CancellationToken ct)
    {
        string fullPath = GetFullPath(directoryPath);

        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult(Result.Success<IReadOnlyList<FileMetadata>>(Array.Empty<FileMetadata>()));
        }

        try
        {
            List<FileMetadata> results = new();
            foreach (string filePath in Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories))
            {
                FileInfo fileInfo = new(filePath);
                string relativePath = Path.GetRelativePath(_basePath, filePath).Replace('\\', '/');
                StoragePath storagePath = StoragePath.From(relativePath);
                string contentType = ResolveContentType(fileInfo.Name);

                results.Add(new FileMetadata(
                    Path: storagePath,
                    SizeBytes: fileInfo.Length,
                    ContentType: contentType,
                    CreatedAt: fileInfo.CreationTimeUtc,
                    ModifiedAt: fileInfo.LastWriteTimeUtc,
                    ETag: null));
            }

            return Task.FromResult<Result<IReadOnlyList<FileMetadata>>>(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list files in {DirectoryPath}", directoryPath.Value);
            return Task.FromResult<Result<IReadOnlyList<FileMetadata>>>(
                Error.Internal($"Failed to list files: {ex.Message}"));
        }
    }

    /// <summary>
    /// Returns a relative URL path for downloading the file.
    /// For local storage, pre-signed URLs are not applicable.
    /// </summary>
    /// <param name="path">The storage path of the file.</param>
    /// <param name="expiresIn">Not used for local storage.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the relative download path.</returns>
    public Task<Result<string>> GetDownloadUrlAsync(StoragePath path, TimeSpan? expiresIn = null, CancellationToken ct = default)
    {
        string fullPath = GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Result<string>>(Error.NotFound($"File not found: {path.Value}"));
        }

        string downloadUrl = $"/api/v1/files/{path.Value}";
        return Task.FromResult<Result<string>>(downloadUrl);
    }

    private string GetFullPath(StoragePath path) =>
        Path.Combine(_basePath, path.Value.Replace('/', Path.DirectorySeparatorChar));

    private string ResolveContentType(string fileName)
    {
        if (_contentTypeProvider.TryGetContentType(fileName, out string? contentType))
        {
            return contentType;
        }

        return "application/octet-stream";
    }
}
