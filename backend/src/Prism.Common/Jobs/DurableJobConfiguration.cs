using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Prism.Common.Jobs;

/// <summary>
/// EF Core entity configuration for <see cref="DurableJob"/>.
/// Maps to the <c>jobs</c> table.
/// </summary>
public sealed class DurableJobConfiguration : IEntityTypeConfiguration<DurableJob>
{
    /// <summary>
    /// Configures the entity mapping for durable jobs.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<DurableJob> builder)
    {
        builder.ToTable("jobs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.JobType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.ParametersJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(4000);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.JobType);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.ProjectId);
    }
}
