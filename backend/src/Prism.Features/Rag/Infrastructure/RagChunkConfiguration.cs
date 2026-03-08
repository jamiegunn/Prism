using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="RagChunk"/>.
/// Maps to the <c>rag_chunks</c> table with pgvector embedding column and HNSW index.
/// </summary>
public sealed class RagChunkConfiguration : IEntityTypeConfiguration<RagChunk>
{
    /// <summary>
    /// Configures the entity mapping, column constraints, vector column, and indexes.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<RagChunk> builder)
    {
        builder.ToTable("rag_chunks");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Content)
            .IsRequired();

        builder.Property(e => e.Embedding)
            .HasColumnType("vector");

        builder.Property(e => e.Metadata)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>(),
                new ValueComparer<Dictionary<string, string>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
                    c => JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode(),
                    c => JsonSerializer.Deserialize<Dictionary<string, string>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));

        // Full-text search support — tsvector generated column for BM25 search
        builder.Property<NpgsqlTypes.NpgsqlTsVector>("SearchVector")
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .IsRequired(false)
            .HasComputedColumnSql("to_tsvector('english', \"Content\")", stored: true);

        builder.HasIndex("SearchVector")
            .HasMethod("gin");

        // Indexes
        builder.HasIndex(e => e.DocumentId);
        builder.HasIndex(e => new { e.DocumentId, e.OrderIndex });
    }
}
