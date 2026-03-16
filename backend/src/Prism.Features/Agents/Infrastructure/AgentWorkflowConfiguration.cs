using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Agents.Domain;

namespace Prism.Features.Agents.Infrastructure;

/// <summary>
/// EF Core configuration for the <see cref="AgentWorkflow"/> entity.
/// </summary>
public sealed class AgentWorkflowConfiguration : IEntityTypeConfiguration<AgentWorkflow>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AgentWorkflow> builder)
    {
        builder.ToTable("agent_workflows");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.SystemPrompt)
            .IsRequired();

        builder.Property(e => e.Model)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Pattern)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.EnabledTools)
            .HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));

        builder.HasMany(e => e.Runs)
            .WithOne()
            .HasForeignKey(r => r.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ProjectId);
        builder.HasIndex(e => e.Name);
    }
}
