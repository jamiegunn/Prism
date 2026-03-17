using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prism.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddRagTraceEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rag_traces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Query = table.Column<string>(type: "text", nullable: false),
                    SearchType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RetrievedChunkCount = table.Column<int>(type: "integer", nullable: false),
                    RetrievedChunksJson = table.Column<string>(type: "jsonb", nullable: false),
                    AssembledContext = table.Column<string>(type: "text", nullable: false),
                    GeneratedResponse = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_traces", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rag_traces_CollectionId",
                table: "rag_traces",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_rag_traces_CreatedAt",
                table: "rag_traces",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rag_traces");
        }
    }
}
