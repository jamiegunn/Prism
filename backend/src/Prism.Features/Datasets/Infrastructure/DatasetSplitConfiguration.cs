using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="DatasetSplit"/>.
/// Maps to the <c>datasets_splits</c> table.
/// </summary>
public sealed class DatasetSplitConfiguration : IEntityTypeConfiguration<DatasetSplit>
{
    /// <summary>
    /// Configures the entity mapping, column constraints, and indexes.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<DatasetSplit> builder)
    {
        builder.ToTable("datasets_splits");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(100)
            .IsRequired();

        // Unique index: one split name per dataset
        builder.HasIndex(e => new { e.DatasetId, e.Name })
            .IsUnique();
    }
}
