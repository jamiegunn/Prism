# ADR-014: Standardized Artifact Model

**Date:** 2026-03-16
**Status:** Accepted
**Deciders:** Project team

## Context

Prism produces many kinds of output: inference run JSON, token trace JSONL, diff artifacts, evaluation result sets, exported prompt versions, RAG ingest manifests, agent traces, notebook exports, and fine-tune dataset packages. Without a unified artifact model, each feature stores outputs in ad hoc formats with inconsistent metadata, making it impossible to:

- Search across artifact types
- Link artifacts to their source entities
- Verify artifact integrity
- Track schema versions for backward compatibility
- Build a unified export/import system

## Decision

Introduce a standardized `Artifact` entity in `Prism.Common/Storage/` with a corresponding `IArtifactStore` interface.

### Artifact Entity

Every important output becomes an artifact with these required fields:

| Field | Type | Description |
|-------|------|-------------|
| `ArtifactId` | `Guid` | Unique identifier |
| `ArtifactType` | `string` | Discriminator: `inference_run`, `token_trace`, `diff`, `evaluation_result`, `prompt_export`, `rag_manifest`, `agent_trace`, `notebook_export`, `finetune_dataset` |
| `WorkspaceId` | `Guid?` | Optional workspace scope |
| `ProjectId` | `Guid?` | Optional project scope |
| `SourceEntityType` | `string` | The entity that produced this artifact (e.g., `InferenceRun`, `EvaluationSuite`) |
| `SourceEntityId` | `Guid` | The ID of the source entity |
| `ContentHash` | `string` | SHA-256 of the artifact content for integrity verification |
| `SchemaVersion` | `string` | Semver of the artifact's internal schema (e.g., `1.0.0`) |
| `MimeType` | `string` | Content type (e.g., `application/json`, `application/jsonl`) |
| `SizeBytes` | `long` | Content size |
| `StoragePath` | `StoragePath` | Location in the file storage system |
| `Metadata` | `JsonDocument?` | Optional key-value metadata |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp |

### IArtifactStore Interface

```csharp
public interface IArtifactStore
{
    Task<Result<Artifact>> StoreAsync(ArtifactCreateRequest request, Stream content, CancellationToken ct);
    Task<Result<Stream>> RetrieveAsync(Guid artifactId, CancellationToken ct);
    Task<Result<IReadOnlyList<Artifact>>> ListAsync(ArtifactQuery query, CancellationToken ct);
    Task<Result> DeleteAsync(Guid artifactId, CancellationToken ct);
    Task<Result<bool>> VerifyIntegrityAsync(Guid artifactId, CancellationToken ct);
}
```

### Design Rules

1. **Features produce artifacts through `IArtifactStore`** — no direct file writes for user-visible outputs.
2. **Content hash is computed on write** — the store computes SHA-256 and stores it; consumers can verify.
3. **Schema version is required** — every artifact type declares its schema version so readers can handle evolution.
4. **Artifacts are immutable** — once stored, content is never modified. New versions create new artifacts.
5. **Storage is delegated to `IFileStorage`** — the artifact store manages metadata; `IFileStorage` handles bytes.

## Consequences

### Positive

- Unified search and listing across all output types
- Integrity verification built in
- Schema versioning enables backward-compatible evolution
- Export/import becomes straightforward — export artifact metadata + content
- Provenance chain: artifact → source entity → project → workspace

### Negative

- Additional indirection for simple file outputs
- Schema version discipline required from feature developers
- Metadata storage adds database rows for every artifact

### Neutral

- Existing `IFileStorage` remains the byte-level storage backend
- Features that currently write files directly will migrate incrementally
- The `StoragePath` value object from ADR-004 is reused

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| Per-feature file storage | Simple, no shared schema | No cross-feature search, no integrity, no schema versioning | Doesn't scale |
| Database BLOB storage | Single system | Large artifacts strain the database, backup complexity | PostgreSQL is not ideal for large binary storage |
| Content-addressable store (like Git) | Deduplication, integrity built in | Complex to implement, overkill for single-user | Over-engineered |

## References

- ADR-004: File Storage Abstraction
- Delivery Plan v2: Section 4.D (Standardize artifact capture)
