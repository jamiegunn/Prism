using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="RagTrace"/>.
/// Maps to the <c>rag_traces</c> table.
/// </summary>
public sealed class RagTraceConfiguration : IEntityTypeConfiguration<RagTrace>
{
    /// <summary>
    /// Configures the entity mapping for RAG traces.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<RagTrace> builder)
    {
        builder.ToTable("rag_traces");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Query)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.SearchType)
            .HasMaxLength(50);

        builder.Property(e => e.RetrievedChunksJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.AssembledContext)
            .HasColumnType("text");

        builder.Property(e => e.GeneratedResponse)
            .HasColumnType("text");

        builder.Property(e => e.Model)
            .HasMaxLength(500);

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(2000);

        builder.HasIndex(e => e.CollectionId);
        builder.HasIndex(e => e.CreatedAt);
    }
}
