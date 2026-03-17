using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prism.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddTraceAndReplaySchemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCost",
                table: "history_records",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExperimentId",
                table: "history_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MeanEntropy",
                table: "history_records",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "history_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PromptVersionId",
                table: "history_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SurpriseTokenCount",
                table: "history_records",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TokensPerSecond",
                table: "history_records",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "history_replay_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplayRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverrideModel = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OverrideTemperature = table.Column<double>(type: "double precision", nullable: true),
                    OverrideMaxTokens = table.Column<int>(type: "integer", nullable: true),
                    OverrideTopP = table.Column<double>(type: "double precision", nullable: true),
                    OverrideInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_history_replay_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_history_replay_runs_history_records_OriginalRecordId",
                        column: x => x.OriginalRecordId,
                        principalTable: "history_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_history_replay_runs_history_records_ReplayRecordId",
                        column: x => x.ReplayRecordId,
                        principalTable: "history_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "history_traces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InferenceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenEventCount = table.Column<int>(type: "integer", nullable: false),
                    Perplexity = table.Column<double>(type: "double precision", nullable: true),
                    MeanEntropy = table.Column<double>(type: "double precision", nullable: true),
                    AverageLogprob = table.Column<double>(type: "double precision", nullable: true),
                    SurpriseTokenCount = table.Column<int>(type: "integer", nullable: false),
                    SurpriseThreshold = table.Column<double>(type: "double precision", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "1.0.0"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_history_traces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_history_traces_history_records_InferenceRecordId",
                        column: x => x.InferenceRecordId,
                        principalTable: "history_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "history_token_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InferenceTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Logprob = table.Column<double>(type: "double precision", nullable: false),
                    Probability = table.Column<double>(type: "double precision", nullable: false),
                    Entropy = table.Column<double>(type: "double precision", nullable: false),
                    IsSurprise = table.Column<bool>(type: "boolean", nullable: false),
                    ByteOffset = table.Column<int>(type: "integer", nullable: true),
                    TopAlternativesJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_history_token_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_history_token_events_history_traces_InferenceTraceId",
                        column: x => x.InferenceTraceId,
                        principalTable: "history_traces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_history_records_ExperimentId",
                table: "history_records",
                column: "ExperimentId");

            migrationBuilder.CreateIndex(
                name: "IX_history_records_ProjectId",
                table: "history_records",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_history_replay_runs_OriginalRecordId",
                table: "history_replay_runs",
                column: "OriginalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_history_replay_runs_ReplayRecordId",
                table: "history_replay_runs",
                column: "ReplayRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_history_token_events_InferenceTraceId_Position",
                table: "history_token_events",
                columns: new[] { "InferenceTraceId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_history_traces_InferenceRecordId",
                table: "history_traces",
                column: "InferenceRecordId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "history_replay_runs");

            migrationBuilder.DropTable(
                name: "history_token_events");

            migrationBuilder.DropTable(
                name: "history_traces");

            migrationBuilder.DropIndex(
                name: "IX_history_records_ExperimentId",
                table: "history_records");

            migrationBuilder.DropIndex(
                name: "IX_history_records_ProjectId",
                table: "history_records");

            migrationBuilder.DropColumn(
                name: "EstimatedCost",
                table: "history_records");

            migrationBuilder.DropColumn(
                name: "ExperimentId",
                table: "history_records");

            migrationBuilder.DropColumn(
                name: "MeanEntropy",
                table: "history_records");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "history_records");

            migrationBuilder.DropColumn(
                name: "PromptVersionId",
                table: "history_records");

            migrationBuilder.DropColumn(
                name: "SurpriseTokenCount",
                table: "history_records");

            migrationBuilder.DropColumn(
                name: "TokensPerSecond",
                table: "history_records");
        }
    }
}
