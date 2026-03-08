using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="PromptTemplate"/>.
/// Maps to the <c>prompts_templates</c> table with feature-prefixed naming.
/// </summary>
public sealed class PromptTemplateConfiguration : IEntityTypeConfiguration<PromptTemplate>
{
    /// <summary>
    /// Configures the entity mapping, column constraints, and indexes for prompt templates.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<PromptTemplate> builder)
    {
        builder.ToTable("prompts_templates");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Category)
            .HasMaxLength(100);

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.Tags)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                new ValueComparer<List<string>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
                    c => JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode(),
                    c => JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));

        builder.HasIndex(e => e.Name);
        builder.HasIndex(e => e.Category);
        builder.HasIndex(e => e.ProjectId);

        builder.HasIndex(e => e.Tags)
            .HasMethod("gin");
    }
}
