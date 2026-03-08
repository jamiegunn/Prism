using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prism.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptLabFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prompts_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Tags = table.Column<string>(type: "jsonb", nullable: false),
                    LatestVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompts_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prompts_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SystemPrompt = table.Column<string>(type: "text", nullable: true),
                    UserTemplate = table.Column<string>(type: "text", nullable: false),
                    Variables = table.Column<string>(type: "jsonb", nullable: false),
                    FewShotExamples = table.Column<string>(type: "jsonb", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompts_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prompts_versions_prompts_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "prompts_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prompts_templates_Category",
                table: "prompts_templates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_prompts_templates_Name",
                table: "prompts_templates",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_prompts_templates_ProjectId",
                table: "prompts_templates",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_prompts_templates_Tags",
                table: "prompts_templates",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_prompts_versions_TemplateId_Version",
                table: "prompts_versions",
                columns: new[] { "TemplateId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prompts_versions");

            migrationBuilder.DropTable(
                name: "prompts_templates");
        }
    }
}
