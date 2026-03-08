using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prism.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchInference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analytics_usage_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PromptTokens = table.Column<int>(type: "integer", nullable: false),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: false),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    TtftMs = table.Column<int>(type: "integer", nullable: true),
                    TokensPerSecond = table.Column<double>(type: "double precision", nullable: true),
                    SourceModule = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_usage_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "batch_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SplitLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PromptVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Parameters = table.Column<string>(type: "jsonb", nullable: false),
                    Concurrency = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    CaptureLogprobs = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Progress = table.Column<double>(type: "double precision", nullable: false),
                    TotalRecords = table.Column<int>(type: "integer", nullable: false),
                    CompletedRecords = table.Column<int>(type: "integer", nullable: false),
                    FailedRecords = table.Column<int>(type: "integer", nullable: false),
                    TokensUsed = table.Column<long>(type: "bigint", nullable: false),
                    Cost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OutputPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "batch_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Input = table.Column<string>(type: "text", nullable: false),
                    Output = table.Column<string>(type: "text", nullable: true),
                    LogprobsData = table.Column<string>(type: "text", nullable: true),
                    Perplexity = table.Column<double>(type: "double precision", nullable: true),
                    TokensUsed = table.Column<int>(type: "integer", nullable: false),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_batch_results_batch_jobs_BatchJobId",
                        column: x => x.BatchJobId,
                        principalTable: "batch_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analytics_usage_logs_CreatedAt",
                table: "analytics_usage_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_analytics_usage_logs_Model",
                table: "analytics_usage_logs",
                column: "Model");

            migrationBuilder.CreateIndex(
                name: "IX_analytics_usage_logs_ProjectId",
                table: "analytics_usage_logs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_analytics_usage_logs_SourceModule",
                table: "analytics_usage_logs",
                column: "SourceModule");

            migrationBuilder.CreateIndex(
                name: "IX_batch_jobs_DatasetId",
                table: "batch_jobs",
                column: "DatasetId");

            migrationBuilder.CreateIndex(
                name: "IX_batch_jobs_Status",
                table: "batch_jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_batch_results_BatchJobId_Status",
                table: "batch_results",
                columns: new[] { "BatchJobId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analytics_usage_logs");

            migrationBuilder.DropTable(
                name: "batch_results");

            migrationBuilder.DropTable(
                name: "batch_jobs");
        }
    }
}
