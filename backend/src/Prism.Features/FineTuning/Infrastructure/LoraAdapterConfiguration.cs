using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.FineTuning.Domain;

namespace Prism.Features.FineTuning.Infrastructure;

/// <summary>
/// EF Core configuration for the <see cref="LoraAdapter"/> entity.
/// </summary>
public sealed class LoraAdapterConfiguration : IEntityTypeConfiguration<LoraAdapter>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LoraAdapter> builder)
    {
        builder.ToTable("finetuning_lora_adapters");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.AdapterPath)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.BaseModel)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(e => e.InstanceId);
        builder.HasIndex(e => e.Name);
    }
}
