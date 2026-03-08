using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="InferenceInstance"/>.
/// Maps to the <c>models_instances</c> table with feature-prefixed naming.
/// </summary>
public sealed class InferenceInstanceConfiguration : IEntityTypeConfiguration<InferenceInstance>
{
    /// <summary>
    /// Configures the entity mapping, column constraints, and indexes for inference instances.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<InferenceInstance> builder)
    {
        builder.ToTable("models_instances");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Endpoint)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.ProviderType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ModelId)
            .HasMaxLength(500);

        builder.Property(e => e.GpuConfig)
            .HasMaxLength(500);

        builder.Property(e => e.LastHealthError)
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

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.ProviderType);
        builder.HasIndex(e => e.IsDefault);
    }
}
