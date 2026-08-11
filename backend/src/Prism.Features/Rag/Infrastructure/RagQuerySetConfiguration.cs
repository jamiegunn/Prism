using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Infrastructure;

/// <summary>
/// EF Core configuration for <see cref="RagQuerySet"/>: table <c>rag_query_sets</c>.
/// </summary>
public sealed class RagQuerySetConfiguration : IEntityTypeConfiguration<RagQuerySet>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RagQuerySet> builder)
    {
        builder.ToTable("rag_query_sets");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey(i => i.QuerySetId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a collection deletes its query sets: chunk ids from a deleted collection
        // label nothing.
        builder.HasOne<RagCollection>()
            .WithMany()
            .HasForeignKey(e => e.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.CollectionId);
    }
}

/// <summary>
/// EF Core configuration for <see cref="RagQuerySetItem"/>: table <c>rag_query_set_items</c>.
/// </summary>
public sealed class RagQuerySetItemConfiguration : IEntityTypeConfiguration<RagQuerySetItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RagQuerySetItem> builder)
    {
        builder.ToTable("rag_query_set_items");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.QueryText)
            .HasMaxLength(4000)
            .IsRequired();

        // uuid[] — Npgsql maps List<Guid> natively; no converter, and the column stays
        // queryable from SQL.
        builder.Property(e => e.RelevantChunkIds)
            .HasColumnType("uuid[]")
            .IsRequired();

        builder.HasIndex(e => e.QuerySetId);
    }
}
