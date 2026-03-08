using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prism.Features.Playground.Domain;

namespace Prism.Features.Playground.Infrastructure;

/// <summary>
/// EF Core entity configuration for <see cref="Conversation"/>.
/// Maps to the <c>playground_conversations</c> table with feature-prefixed naming.
/// </summary>
public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    /// <summary>
    /// Configures the entity mapping, column constraints, and indexes for playground conversations.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("playground_conversations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.SystemPrompt)
            .HasColumnType("text");

        builder.Property(e => e.ModelId)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.Parameters)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<ConversationParameters>(v, (JsonSerializerOptions?)null) ?? new ConversationParameters(),
                new ValueComparer<ConversationParameters>(
                    (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
                    c => JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode(),
                    c => JsonSerializer.Deserialize<ConversationParameters>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));

        builder.HasMany(e => e.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.LastMessageAt);
        builder.HasIndex(e => e.ModelId);
        builder.HasIndex(e => e.IsPinned);
    }
}
