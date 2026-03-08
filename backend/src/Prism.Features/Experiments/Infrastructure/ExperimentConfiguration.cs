using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="Experiment"/>.
/// Maps to the <c>experiments_experiments</c> table with feature-prefixed naming.
/// </summary>
public sealed class ExperimentConfiguration : IEntityTypeConfiguration<Experiment>
{
    /// <summary>
    /// Configures the entity mapping, column constraints, relationships, and indexes for experiments.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Experiment> builder)
    {
        builder.ToTable("experiments_experiments");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.Hypothesis)
            .HasColumnType("text");

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne(e => e.Project)
            .WithMany(p => p.Experiments)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.ProjectId, e.CreatedAt });
        builder.HasIndex(e => e.Status);
    }
}
