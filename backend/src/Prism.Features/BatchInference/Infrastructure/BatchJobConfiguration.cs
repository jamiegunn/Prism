using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.BatchInference.Domain;

namespace Prism.Features.BatchInference.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="BatchJob"/>.
/// Maps to the <c>batch_jobs</c> table.
/// </summary>
public sealed class BatchJobConfiguration : IEntityTypeConfiguration<BatchJob>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BatchJob> builder)
    {
        builder.ToTable("batch_jobs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Model)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.SplitLabel)
            .HasMaxLength(50);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(4000);

        builder.Property(e => e.OutputPath)
            .HasMaxLength(1000);

        builder.Property(e => e.Cost)
            .HasPrecision(18, 6);

        // Parameters — jsonb
        builder.Property(e => e.Parameters)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object?>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object?>(),
                new ValueComparer<Dictionary<string, object?>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
                    c => JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode(),
                    c => JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));

        // Relationships
        builder.HasMany(e => e.Results)
            .WithOne()
            .HasForeignKey(r => r.BatchJobId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.DatasetId);
    }
}
