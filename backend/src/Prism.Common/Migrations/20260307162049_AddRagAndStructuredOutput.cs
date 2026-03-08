using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;
using Pgvector;

#nullable disable

namespace Prism.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddRagAndStructuredOutput : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "rag_collections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EmbeddingModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Dimensions = table.Column<int>(type: "integer", nullable: false),
                    DistanceMetric = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChunkingStrategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChunkSize = table.Column<int>(type: "integer", nullable: false),
                    ChunkOverlap = table.Column<int>(type: "integer", nullable: false),
                    DocumentCount = table.Column<int>(type: "integer", nullable: false),
                    ChunkCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_collections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "structured_output_schemas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SchemaJson = table.Column<string>(type: "jsonb", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_structured_output_schemas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rag_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Filename = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ChunkCount = table.Column<int>(type: "integer", nullable: false),
                    CharacterCount = table.Column<int>(type: "integer", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rag_documents_rag_collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "rag_collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rag_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: false),
                    StartOffset = table.Column<int>(type: "integer", nullable: false),
                    EndOffset = table.Column<int>(type: "integer", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('english', \"Content\")", stored: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rag_chunks_rag_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "rag_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rag_chunks_DocumentId",
                table: "rag_chunks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_rag_chunks_DocumentId_OrderIndex",
                table: "rag_chunks",
                columns: new[] { "DocumentId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_rag_chunks_search_vector",
                table: "rag_chunks",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_rag_collections_Name",
                table: "rag_collections",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_rag_collections_ProjectId",
                table: "rag_collections",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_rag_documents_CollectionId",
                table: "rag_documents",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_rag_documents_Status",
                table: "rag_documents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_structured_output_schemas_Name",
                table: "structured_output_schemas",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_structured_output_schemas_ProjectId",
                table: "structured_output_schemas",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rag_chunks");

            migrationBuilder.DropTable(
                name: "structured_output_schemas");

            migrationBuilder.DropTable(
                name: "rag_documents");

            migrationBuilder.DropTable(
                name: "rag_collections");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
