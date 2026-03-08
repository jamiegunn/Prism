using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="Run"/>.
/// Maps to the <c>experiments_runs</c> table with feature-prefixed naming.
/// </summary>
public sealed class RunConfiguration : IEntityTypeConfiguration<Run>
{
    /// <summary>
    /// Configures the entity mapping, column constraints, relationships, jsonb columns, and indexes for experiment runs.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Run> builder)
    {
        builder.ToTable("experiments_runs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200);

        builder.Property(e => e.Model)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.Input)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.Output)
            .HasColumnType("text");

        builder.Property(e => e.SystemPrompt)
            .HasColumnType("text");

        builder.Property(e => e.LogprobsData)
            .HasColumnType("jsonb");

        builder.Property(e => e.Error)
            .HasColumnType("text");

        builder.Property(e => e.FinishReason)
            .HasMaxLength(50);

        builder.Property(e => e.Cost)
            .HasPrecision(18, 8);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // RunParameters — jsonb with ValueComparer
        builder.Property(e => e.Parameters)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<RunParameters>(v, (JsonSerializerOptions?)null) ?? new RunParameters(),
                new ValueComparer<RunParameters>(
                    (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
                    c => JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode(),
                    c => JsonSerializer.Deserialize<RunParameters>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));

        // Metrics — jsonb Dictionary<string, double> with ValueComparer
        builder.Property(e => e.Metrics)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, double>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, double>(),
                new ValueComparer<Dictionary<string, double>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
                    c => JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode(),
                    c => JsonSerializer.Deserialize<Dictionary<string, double>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));

        // Tags — jsonb string array with ValueComparer
        builder.Property(e => e.Tags)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                new ValueComparer<List<string>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
                    c => JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode(),
                    c => JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));

        // Relationships
        builder.HasOne(e => e.Experiment)
            .WithMany(exp => exp.Runs)
            .HasForeignKey(e => e.ExperimentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => new { e.ExperimentId, e.CreatedAt });
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.Model);

        // GIN indexes for jsonb columns
        builder.HasIndex(e => e.Metrics)
            .HasMethod("gin");

        builder.HasIndex(e => e.Tags)
            .HasMethod("gin");
    }
}
