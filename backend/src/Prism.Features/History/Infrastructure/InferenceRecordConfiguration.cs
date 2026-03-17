using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.History.Domain;

namespace Prism.Features.History.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="InferenceRecord"/>.
/// Maps to the <c>history_records</c> table with feature-prefixed naming.
/// </summary>
public sealed class InferenceRecordConfiguration : IEntityTypeConfiguration<InferenceRecord>
{
    /// <summary>
    /// Configures the entity mapping, column constraints, indexes, and value conversions
    /// for inference history records.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<InferenceRecord> builder)
    {
        builder.ToTable("history_records");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SourceModule)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ProviderName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.ProviderType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ProviderEndpoint)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.Model)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.RequestJson)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.ResponseJson)
            .HasColumnType("text");

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(e => e.Tags)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                new ValueComparer<List<string>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

        builder.Property(e => e.EnvironmentJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.EstimatedCost)
            .HasPrecision(18, 8);

        builder.HasIndex(e => e.SourceModule);
        builder.HasIndex(e => e.Model);
        builder.HasIndex(e => e.StartedAt);
        builder.HasIndex(e => e.IsSuccess);
        builder.HasIndex(e => e.ProjectId);
        builder.HasIndex(e => e.ExperimentId);
    }
}
