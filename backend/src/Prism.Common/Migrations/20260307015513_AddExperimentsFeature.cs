using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prism.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddExperimentsFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "experiments_projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experiments_projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "experiments_experiments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Hypothesis = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experiments_experiments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_experiments_experiments_experiments_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "experiments_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "experiments_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperimentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Model = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Parameters = table.Column<string>(type: "jsonb", nullable: false),
                    PromptVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Input = table.Column<string>(type: "text", nullable: false),
                    Output = table.Column<string>(type: "text", nullable: true),
                    SystemPrompt = table.Column<string>(type: "text", nullable: true),
                    Metrics = table.Column<string>(type: "jsonb", nullable: false),
                    PromptTokens = table.Column<int>(type: "integer", nullable: false),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    Cost = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    TtftMs = table.Column<int>(type: "integer", nullable: true),
                    TokensPerSecond = table.Column<double>(type: "double precision", nullable: true),
                    Perplexity = table.Column<double>(type: "double precision", nullable: true),
                    LogprobsData = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string>(type: "jsonb", nullable: false),
                    FinishReason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experiments_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_experiments_runs_experiments_experiments_ExperimentId",
                        column: x => x.ExperimentId,
                        principalTable: "experiments_experiments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_experiments_experiments_ProjectId_CreatedAt",
                table: "experiments_experiments",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_experiments_experiments_Status",
                table: "experiments_experiments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_experiments_projects_CreatedAt",
                table: "experiments_projects",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_experiments_projects_IsArchived",
                table: "experiments_projects",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_experiments_runs_ExperimentId_CreatedAt",
                table: "experiments_runs",
                columns: new[] { "ExperimentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_experiments_runs_Metrics",
                table: "experiments_runs",
                column: "Metrics")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_experiments_runs_Model",
                table: "experiments_runs",
                column: "Model");

            migrationBuilder.CreateIndex(
                name: "IX_experiments_runs_Status",
                table: "experiments_runs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_experiments_runs_Tags",
                table: "experiments_runs",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "experiments_runs");

            migrationBuilder.DropTable(
                name: "experiments_experiments");

            migrationBuilder.DropTable(
                name: "experiments_projects");
        }
    }
}
