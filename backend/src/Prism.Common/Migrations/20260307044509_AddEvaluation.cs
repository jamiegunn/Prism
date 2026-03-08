using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prism.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evaluation_evaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SplitLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Models = table.Column<string>(type: "jsonb", nullable: false),
                    PromptVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScoringMethods = table.Column<string>(type: "jsonb", nullable: false),
                    Config = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Progress = table.Column<double>(type: "double precision", nullable: false),
                    TotalRecords = table.Column<int>(type: "integer", nullable: false),
                    CompletedRecords = table.Column<int>(type: "integer", nullable: false),
                    FailedRecords = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_evaluations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Input = table.Column<string>(type: "text", nullable: false),
                    ExpectedOutput = table.Column<string>(type: "text", nullable: true),
                    ActualOutput = table.Column<string>(type: "text", nullable: true),
                    Scores = table.Column<string>(type: "jsonb", nullable: false),
                    LogprobsData = table.Column<string>(type: "text", nullable: true),
                    Perplexity = table.Column<double>(type: "double precision", nullable: true),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    PromptTokens = table.Column<int>(type: "integer", nullable: false),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evaluation_results_evaluation_evaluations_EvaluationId",
                        column: x => x.EvaluationId,
                        principalTable: "evaluation_evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_evaluations_DatasetId",
                table: "evaluation_evaluations",
                column: "DatasetId");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_evaluations_ProjectId",
                table: "evaluation_evaluations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_evaluations_Status",
                table: "evaluation_evaluations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_results_EvaluationId_Model",
                table: "evaluation_results",
                columns: new[] { "EvaluationId", "Model" });

            migrationBuilder.CreateIndex(
                name: "IX_evaluation_results_Scores",
                table: "evaluation_results",
                column: "Scores")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_results");

            migrationBuilder.DropTable(
                name: "evaluation_evaluations");
        }
    }
}
