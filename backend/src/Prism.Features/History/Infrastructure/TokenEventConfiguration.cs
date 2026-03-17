using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.History.Domain;

namespace Prism.Features.History.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="TokenEvent"/>.
/// Maps to the <c>history_token_events</c> table with feature-prefixed naming.
/// </summary>
public sealed class TokenEventConfiguration : IEntityTypeConfiguration<TokenEvent>
{
    /// <summary>
    /// Configures the entity mapping for token events.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<TokenEvent> builder)
    {
        builder.ToTable("history_token_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Token)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.TopAlternativesJson)
            .HasColumnType("jsonb");

        builder.HasIndex(e => new { e.InferenceTraceId, e.Position });
    }
}
