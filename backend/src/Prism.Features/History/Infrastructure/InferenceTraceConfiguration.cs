using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.History.Domain;

namespace Prism.Features.History.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="InferenceTrace"/>.
/// Maps to the <c>history_traces</c> table with feature-prefixed naming.
/// </summary>
public sealed class InferenceTraceConfiguration : IEntityTypeConfiguration<InferenceTrace>
{
    /// <summary>
    /// Configures the entity mapping for inference traces.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<InferenceTrace> builder)
    {
        builder.ToTable("history_traces");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SchemaVersion)
            .HasMaxLength(20)
            .HasDefaultValue("1.0.0");

        builder.HasOne(e => e.InferenceRecord)
            .WithOne(r => r.Trace)
            .HasForeignKey<InferenceTrace>(e => e.InferenceRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.TokenEvents)
            .WithOne(te => te.InferenceTrace)
            .HasForeignKey(te => te.InferenceTraceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.InferenceRecordId)
            .IsUnique();
    }
}
