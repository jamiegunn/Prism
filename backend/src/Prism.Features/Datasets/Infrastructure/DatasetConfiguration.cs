using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="Dataset"/>.
/// Maps to the <c>datasets_datasets</c> table with feature-prefixed naming.
/// </summary>
public sealed class DatasetConfiguration : IEntityTypeConfiguration<Dataset>
{
    /// <summary>
    /// Configures the entity mapping, column constraints, relationships, and indexes.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Dataset> builder)
    {
        builder.ToTable("datasets_datasets");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.Format)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // Schema — jsonb List<ColumnSchema> with ValueComparer
        builder.Property(e => e.Schema)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<ColumnSchema>>(v, (JsonSerializerOptions?)null) ?? new List<ColumnSchema>(),
                new ValueComparer<List<ColumnSchema>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
                    c => JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode(),
                    c => JsonSerializer.Deserialize<List<ColumnSchema>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));

        // Relationships
        builder.HasMany(e => e.Records)
            .WithOne()
            .HasForeignKey(r => r.DatasetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Splits)
            .WithOne()
            .HasForeignKey(s => s.DatasetId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.ProjectId);
        builder.HasIndex(e => e.Name);
    }
}
