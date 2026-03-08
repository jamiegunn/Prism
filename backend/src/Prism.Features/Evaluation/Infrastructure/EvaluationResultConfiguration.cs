using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Evaluation.Domain;

namespace Prism.Features.Evaluation.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="EvaluationResult"/>.
/// Maps to the <c>evaluation_results</c> table.
/// </summary>
public sealed class EvaluationResultConfiguration : IEntityTypeConfiguration<EvaluationResult>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EvaluationResult> builder)
    {
        builder.ToTable("evaluation_results");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Model)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Input)
            .IsRequired();

        // Scores — jsonb Dict<string, double> with GIN index
        builder.Property(e => e.Scores)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, double>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, double>(),
                new ValueComparer<Dictionary<string, double>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
                    c => JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode(),
                    c => JsonSerializer.Deserialize<Dictionary<string, double>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));

        builder.Property(e => e.Error)
            .HasMaxLength(4000);

        // Indexes
        builder.HasIndex(e => new { e.EvaluationId, e.Model });
        builder.HasIndex(e => e.Scores).HasMethod("gin");
    }
}
