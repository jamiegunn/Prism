using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Playground.Domain;

namespace Prism.Features.Playground.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="Message"/>.
/// Maps to the <c>playground_messages</c> table with feature-prefixed naming.
/// </summary>
public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    /// <summary>
    /// Configures the entity mapping, column constraints, and indexes for playground messages.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("playground_messages");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Role)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Content)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.LogprobsJson)
            .HasColumnType("text");

        builder.Property(e => e.FinishReason)
            .HasMaxLength(100);

        builder.HasIndex(e => new { e.ConversationId, e.SortOrder });
    }
}
