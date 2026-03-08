using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.BatchInference.Domain;

namespace Prism.Features.BatchInference.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="BatchResult"/>.
/// Maps to the <c>batch_results</c> table.
/// </summary>
public sealed class BatchResultConfiguration : IEntityTypeConfiguration<BatchResult>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BatchResult> builder)
    {
        builder.ToTable("batch_results");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Input)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Error)
            .HasMaxLength(4000);

        // Indexes
        builder.HasIndex(e => new { e.BatchJobId, e.Status });
    }
}
