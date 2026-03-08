using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="RagCollection"/>.
/// Maps to the <c>rag_collections</c> table with feature-prefixed naming.
/// </summary>
public sealed class RagCollectionConfiguration : IEntityTypeConfiguration<RagCollection>
{
    /// <summary>
    /// Configures the entity mapping, column constraints, relationships, and indexes.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<RagCollection> builder)
    {
        builder.ToTable("rag_collections");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.EmbeddingModel)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.DistanceMetric)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ChunkingStrategy)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasMany(e => e.Documents)
            .WithOne()
            .HasForeignKey(d => d.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ProjectId);
        builder.HasIndex(e => e.Name);
    }
}
