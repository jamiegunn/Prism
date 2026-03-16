using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Agents.Domain;

namespace Prism.Features.Agents.Infrastructure;

/// <summary>
/// EF Core configuration for the <see cref="AgentRun"/> entity.
/// </summary>
public sealed class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        builder.ToTable("agent_runs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Input)
            .IsRequired();

        builder.Property(e => e.StepsJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(e => e.WorkflowId);
        builder.HasIndex(e => e.Status);
    }
}
