using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="DatasetRecord"/>.
/// Maps to the <c>datasets_records</c> table with JSONB data column and GIN index.
/// </summary>
public sealed class DatasetRecordConfiguration : IEntityTypeConfiguration<DatasetRecord>
{
    /// <summary>
    /// Configures the entity mapping, column constraints, and indexes.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<DatasetRecord> builder)
    {
        builder.ToTable("datasets_records");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SplitLabel)
            .HasMaxLength(50);

        // Data — jsonb Dictionary<string, object?> with ValueComparer
        builder.Property(e => e.Data)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object?>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object?>(),
                new ValueComparer<Dictionary<string, object?>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
                    c => JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode(),
                    c => JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));

        // Indexes
        builder.HasIndex(e => new { e.DatasetId, e.SplitLabel });
        builder.HasIndex(e => new { e.DatasetId, e.OrderIndex });

        // GIN index for jsonb data querying
        builder.HasIndex(e => e.Data)
            .HasMethod("gin");
    }
}
