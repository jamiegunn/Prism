using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="Project"/>.
/// Maps to the <c>experiments_projects</c> table with feature-prefixed naming.
/// </summary>
public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    /// <summary>
    /// Configures the entity mapping, column constraints, and indexes for experiment projects.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("experiments_projects");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.HasIndex(e => e.IsArchived);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.WorkspaceId);
    }
}
