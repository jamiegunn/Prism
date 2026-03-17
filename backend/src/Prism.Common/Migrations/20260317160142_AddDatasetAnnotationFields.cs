using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prism.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddDatasetAnnotationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AnnotatedAt",
                table: "datasets_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnotationLabel",
                table: "datasets_records",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnnotationNote",
                table: "datasets_records",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCorrect",
                table: "datasets_records",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_datasets_records_DatasetId_AnnotationLabel",
                table: "datasets_records",
                columns: new[] { "DatasetId", "AnnotationLabel" });

            migrationBuilder.CreateIndex(
                name: "IX_datasets_records_DatasetId_IsCorrect",
                table: "datasets_records",
                columns: new[] { "DatasetId", "IsCorrect" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_datasets_records_DatasetId_AnnotationLabel",
                table: "datasets_records");

            migrationBuilder.DropIndex(
                name: "IX_datasets_records_DatasetId_IsCorrect",
                table: "datasets_records");

            migrationBuilder.DropColumn(
                name: "AnnotatedAt",
                table: "datasets_records");

            migrationBuilder.DropColumn(
                name: "AnnotationLabel",
                table: "datasets_records");

            migrationBuilder.DropColumn(
                name: "AnnotationNote",
                table: "datasets_records");

            migrationBuilder.DropColumn(
                name: "IsCorrect",
                table: "datasets_records");
        }
    }
}
