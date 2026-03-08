using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.StructuredOutput.Domain;

namespace Prism.Features.StructuredOutput.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="JsonSchemaEntity"/>.
/// Maps to the <c>structured_output_schemas</c> table.
/// </summary>
public sealed class JsonSchemaEntityConfiguration : IEntityTypeConfiguration<JsonSchemaEntity>
{
    /// <summary>
    /// Configures the entity mapping, column constraints, and indexes.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<JsonSchemaEntity> builder)
    {
        builder.ToTable("structured_output_schemas");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.SchemaJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(e => e.ProjectId);
        builder.HasIndex(e => e.Name);
    }
}
