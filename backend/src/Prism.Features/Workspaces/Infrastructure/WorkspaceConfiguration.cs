using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Workspaces.Domain;

namespace Prism.Features.Workspaces.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="Workspace"/>.
/// Maps to the <c>workspaces</c> table.
/// </summary>
public sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    /// <summary>
    /// Configures the entity mapping for workspaces.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("workspaces");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.IconColor)
            .HasMaxLength(50);

        builder.HasIndex(e => e.Name);
        builder.HasIndex(e => e.IsDefault);
    }
}
