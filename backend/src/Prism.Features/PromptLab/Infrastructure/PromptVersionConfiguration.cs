using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="PromptVersion"/>.
/// Maps to the <c>prompts_versions</c> table with feature-prefixed naming.
/// </summary>
public sealed class PromptVersionConfiguration : IEntityTypeConfiguration<PromptVersion>
{
    /// <summary>
    /// Configures the entity mapping, column constraints, relationships, and indexes for prompt versions.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<PromptVersion> builder)
    {
        builder.ToTable("prompts_versions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SystemPrompt)
            .HasColumnType("text");

        builder.Property(e => e.UserTemplate)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        // Variables — jsonb List<PromptVariable> with ValueComparer
        builder.Property(e => e.Variables)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<PromptVariable>>(v, (JsonSerializerOptions?)null) ?? new List<PromptVariable>(),
                new ValueComparer<List<PromptVariable>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
                    c => JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode(),
                    c => JsonSerializer.Deserialize<List<PromptVariable>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));

        // FewShotExamples — jsonb List<FewShotExample> with ValueComparer
        builder.Property(e => e.FewShotExamples)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<FewShotExample>>(v, (JsonSerializerOptions?)null) ?? new List<FewShotExample>(),
                new ValueComparer<List<FewShotExample>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
                    c => JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode(),
                    c => JsonSerializer.Deserialize<List<FewShotExample>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));

        // Relationships
        builder.HasOne(e => e.Template)
            .WithMany(t => t.Versions)
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique index: one version number per template
        builder.HasIndex(e => new { e.TemplateId, e.Version })
            .IsUnique();
    }
}
