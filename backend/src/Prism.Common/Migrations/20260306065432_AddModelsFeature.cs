using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prism.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddModelsFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "models_instances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProviderType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ModelId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GpuConfig = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MaxContextLength = table.Column<int>(type: "integer", nullable: true),
                    SupportsLogprobs = table.Column<bool>(type: "boolean", nullable: false),
                    MaxTopLogprobs = table.Column<int>(type: "integer", nullable: false),
                    SupportsStreaming = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsMetrics = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsTokenize = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsGuidedDecoding = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsMultimodal = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsModelSwap = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    LastHealthCheck = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastHealthError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Tags = table.Column<List<string>>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_models_instances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_models_instances_IsDefault",
                table: "models_instances",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_models_instances_ProviderType",
                table: "models_instances",
                column: "ProviderType");

            migrationBuilder.CreateIndex(
                name: "IX_models_instances_Status",
                table: "models_instances",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "models_instances");
        }
    }
}
