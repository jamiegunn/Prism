using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Notebooks.Domain;

namespace Prism.Features.Notebooks.Infrastructure;

/// <summary>
/// EF Core configuration for the <see cref="Notebook"/> entity.
/// </summary>
public sealed class NotebookConfiguration : IEntityTypeConfiguration<Notebook>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Notebook> builder)
    {
        builder.ToTable("notebooks");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.Content)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.KernelName)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(e => e.ProjectId);
        builder.HasIndex(e => e.Name);
    }
}
