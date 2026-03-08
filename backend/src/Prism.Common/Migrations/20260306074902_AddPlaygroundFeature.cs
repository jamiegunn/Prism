using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prism.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaygroundFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "playground_conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SystemPrompt = table.Column<string>(type: "text", nullable: true),
                    ModelId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Parameters = table.Column<string>(type: "jsonb", nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playground_conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "playground_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: true),
                    LogprobsJson = table.Column<string>(type: "text", nullable: true),
                    Perplexity = table.Column<double>(type: "double precision", nullable: true),
                    LatencyMs = table.Column<int>(type: "integer", nullable: true),
                    TtftMs = table.Column<int>(type: "integer", nullable: true),
                    TokensPerSecond = table.Column<double>(type: "double precision", nullable: true),
                    FinishReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_playground_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_playground_messages_playground_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "playground_conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_playground_conversations_IsPinned",
                table: "playground_conversations",
                column: "IsPinned");

            migrationBuilder.CreateIndex(
                name: "IX_playground_conversations_LastMessageAt",
                table: "playground_conversations",
                column: "LastMessageAt");

            migrationBuilder.CreateIndex(
                name: "IX_playground_conversations_ModelId",
                table: "playground_conversations",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_playground_messages_ConversationId_SortOrder",
                table: "playground_messages",
                columns: new[] { "ConversationId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "playground_messages");

            migrationBuilder.DropTable(
                name: "playground_conversations");
        }
    }
}
