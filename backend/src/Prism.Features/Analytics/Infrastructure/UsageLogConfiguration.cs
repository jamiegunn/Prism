using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Analytics.Domain;

namespace Prism.Features.Analytics.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="UsageLog"/>.
/// Maps to the <c>analytics_usage_logs</c> table.
/// </summary>
public sealed class UsageLogConfiguration : IEntityTypeConfiguration<UsageLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UsageLog> builder)
    {
        builder.ToTable("analytics_usage_logs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Model)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.SourceModule)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Cost)
            .HasPrecision(18, 6);

        // Indexes for common query patterns
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.SourceModule);
        builder.HasIndex(e => e.Model);
        builder.HasIndex(e => e.ProjectId);
    }
}
