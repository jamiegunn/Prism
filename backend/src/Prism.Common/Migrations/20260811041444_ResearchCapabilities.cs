using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prism.Common.Migrations
{
    /// <inheritdoc />
    public partial class ResearchCapabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-edited: a bare ALTER COLUMN TYPE cannot cast jsonb to text[], and Postgres
            // rejects subqueries inside a USING transform — so the conversion is
            // add-backfill-swap. Data survives: each stored JSON array's elements become the
            // array's elements.
            migrationBuilder.Sql(
                """
                ALTER TABLE history_records ADD COLUMN "Tags_new" text[] NOT NULL DEFAULT ARRAY[]::text[];

                UPDATE history_records
                SET "Tags_new" = CASE
                    WHEN "Tags" IS NOT NULL AND jsonb_typeof("Tags") = 'array'
                        THEN COALESCE(
                            (SELECT array_agg(value) FROM jsonb_array_elements_text("Tags")),
                            ARRAY[]::text[])
                    ELSE ARRAY[]::text[]
                END;

                ALTER TABLE history_records DROP COLUMN "Tags";
                ALTER TABLE history_records RENAME COLUMN "Tags_new" TO "Tags";
                ALTER TABLE history_records ALTER COLUMN "Tags" DROP DEFAULT;
                """);

            migrationBuilder.AddColumn<string>(
                name: "SpanId",
                table: "history_records",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                table: "history_records",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            // Hand-edited: the scaffolded default was the empty string, which is not valid
            // jsonb; existing rows get an empty object instead.
            migrationBuilder.AddColumn<string>(
                name: "ScoreDefinitions",
                table: "evaluation_evaluations",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateTable(
                name: "rag_query_sets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_query_sets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rag_query_sets_rag_collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "rag_collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rag_query_set_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuerySetId = table.Column<Guid>(type: "uuid", nullable: false),
                    QueryText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RelevantChunkIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_query_set_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rag_query_set_items_rag_query_sets_QuerySetId",
                        column: x => x.QuerySetId,
                        principalTable: "rag_query_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rag_query_set_items_QuerySetId",
                table: "rag_query_set_items",
                column: "QuerySetId");

            migrationBuilder.CreateIndex(
                name: "IX_rag_query_sets_CollectionId",
                table: "rag_query_sets",
                column: "CollectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rag_query_set_items");

            migrationBuilder.DropTable(
                name: "rag_query_sets");

            migrationBuilder.DropColumn(
                name: "SpanId",
                table: "history_records");

            migrationBuilder.DropColumn(
                name: "TraceId",
                table: "history_records");

            migrationBuilder.DropColumn(
                name: "ScoreDefinitions",
                table: "evaluation_evaluations");

            // Hand-edited to mirror Up: converts the text[] back to a jsonb array
            // (to_jsonb over an array needs no subquery, so plain USING works here).
            migrationBuilder.Sql(
                """
                ALTER TABLE history_records
                ALTER COLUMN "Tags" TYPE jsonb
                USING (to_jsonb("Tags"));
                """);
        }
    }
}
