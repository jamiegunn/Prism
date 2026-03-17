using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.History.Domain;

namespace Prism.Features.History.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="ReplayRun"/>.
/// Maps to the <c>history_replay_runs</c> table with feature-prefixed naming.
/// </summary>
public sealed class ReplayRunConfiguration : IEntityTypeConfiguration<ReplayRun>
{
    /// <summary>
    /// Configures the entity mapping for replay runs.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<ReplayRun> builder)
    {
        builder.ToTable("history_replay_runs");

        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.OriginalRecord)
            .WithMany()
            .HasForeignKey(e => e.OriginalRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ReplayRecord)
            .WithMany()
            .HasForeignKey(e => e.ReplayRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.OverrideModel)
            .HasMaxLength(500);

        builder.HasIndex(e => e.OriginalRecordId);
        builder.HasIndex(e => e.ReplayRecordId)
            .IsUnique();
    }
}
