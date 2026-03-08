using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prism.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddDatasets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "datasets_datasets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Schema = table.Column<string>(type: "jsonb", nullable: false),
                    RecordCount = table.Column<int>(type: "integer", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_datasets_datasets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "datasets_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    SplitLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_datasets_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_datasets_records_datasets_datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "datasets_datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "datasets_splits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_datasets_splits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_datasets_splits_datasets_datasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "datasets_datasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_datasets_datasets_Name",
                table: "datasets_datasets",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_datasets_datasets_ProjectId",
                table: "datasets_datasets",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_datasets_records_Data",
                table: "datasets_records",
                column: "Data")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_datasets_records_DatasetId_OrderIndex",
                table: "datasets_records",
                columns: new[] { "DatasetId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_datasets_records_DatasetId_SplitLabel",
                table: "datasets_records",
                columns: new[] { "DatasetId", "SplitLabel" });

            migrationBuilder.CreateIndex(
                name: "IX_datasets_splits_DatasetId_Name",
                table: "datasets_splits",
                columns: new[] { "DatasetId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "datasets_records");

            migrationBuilder.DropTable(
                name: "datasets_splits");

            migrationBuilder.DropTable(
                name: "datasets_datasets");
        }
    }
}
