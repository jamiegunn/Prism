# ADR-004: File Storage Abstraction

**Date:** 2026-03-05
**Status:** Accepted
**Deciders:** Project team

## Context

The platform stores datasets, model artifacts, exported results, notebook files, and evaluation outputs. Initially these live on the local filesystem, but future deployments may target Azure Blob Storage, AWS S3, or other block storage providers.

Direct `System.IO.File` calls throughout feature code would make a storage migration a large-scale rewrite.

## Decision

Introduce `IFileStorage` as the file storage interface with a `StoragePath` value object:

```csharp
public interface IFileStorage
{
    Task<Result<FileMetadata>> StoreAsync(StoragePath path, Stream content, string contentType, CancellationToken ct);
    Task<Result<Stream>> RetrieveAsync(StoragePath path, CancellationToken ct);
    Task<Result> DeleteAsync(StoragePath path, CancellationToken ct);
    Task<Result<bool>> ExistsAsync(StoragePath path, CancellationToken ct);
    Task<Result<FileMetadata>> GetMetadataAsync(StoragePath path, CancellationToken ct);
    Task<Result<IReadOnlyList<FileMetadata>>> ListAsync(StoragePath prefix, CancellationToken ct);
    Task<Result<string>> GetDownloadUrlAsync(StoragePath path, TimeSpan expiry, CancellationToken ct);
}
```

`StoragePath` is a value object that normalizes paths (forward slashes, no leading slash, validated segments) and prevents path traversal attacks.

Implementations:

| Provider | Config Value | Use Case |
|----------|-------------|----------|
| `LocalFileStorage` | `"Local"` | Default — local filesystem with configurable base path |
| `AzureBlobStorage` | `"AzureBlob"` | Azure Blob Storage |
| `S3Storage` | `"S3"` | AWS S3 |

Provider is selected via configuration: `"Storage:Provider": "Local"`.

## Consequences

### Positive

- Feature code never touches `System.IO` directly — all file access goes through `IFileStorage`
- Swap from local to cloud storage by changing config + adding connection strings
- `StoragePath` prevents path traversal and normalizes cross-platform path differences
- `GetDownloadUrlAsync` supports pre-signed URLs for cloud, file:// URIs for local
- All operations return `Result<T>` — consistent with platform error handling (ADR-002)

### Negative

- Local filesystem doesn't natively support pre-signed URLs — `GetDownloadUrlAsync` returns file paths or localhost URLs
- Stream-based API means no direct file path access — features that need a physical file path (e.g., passing to external tools) need a temporary file step
- Extra abstraction over what starts as simple file I/O

### Neutral

- `FileMetadata` includes: `StoragePath`, `ContentType`, `SizeBytes`, `CreatedAtUtc`, `LastModifiedUtc`, `ETag`
- Base path for local storage is configurable: `"Storage:Local:BasePath": "./data"`
- Directories are implicit — `ListAsync` with a prefix simulates directory listing

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| `System.IO.File` directly | Zero abstraction, maximum performance | Locked to local filesystem, rewrite needed for cloud | Violates provider abstraction principle |
| `System.IO.Abstractions` | Community library for filesystem testing | Only abstracts local filesystem, no cloud support | Doesn't solve the cloud migration problem |
| Cloud SDK from day one (e.g., Azure SDK) | Ready for cloud | Overkill for local-first, requires cloud credentials in dev | Premature; local is the primary target for now |

## References

- See `ARCHITECTURE.md` — File Storage Abstraction section
- `StoragePath` is defined in `Common/Storage/StoragePath.cs`
