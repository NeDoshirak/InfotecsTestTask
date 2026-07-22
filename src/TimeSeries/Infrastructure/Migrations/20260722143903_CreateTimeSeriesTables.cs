using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSeries.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateTimeSeriesTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UploadedFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    NormalizedFileName = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentHash = table.Column<string>(type: "text", nullable: false),
                    RowsCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadedFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DateDeltaSeconds = table.Column<double>(type: "double precision", nullable: false),
                    FirstOperationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AverageExecutionTime = table.Column<double>(type: "double precision", nullable: false),
                    AverageValue = table.Column<double>(type: "double precision", nullable: false),
                    MedianValue = table.Column<double>(type: "double precision", nullable: false),
                    MaxValue = table.Column<double>(type: "double precision", nullable: false),
                    MinValue = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessingResults_UploadedFiles_UploadedFileId",
                        column: x => x.UploadedFileId,
                        principalTable: "UploadedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Values",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExecutionTime = table.Column<double>(type: "double precision", nullable: false),
                    IndicatorValue = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Values", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Values_UploadedFiles_UploadedFileId",
                        column: x => x.UploadedFileId,
                        principalTable: "UploadedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingResults_AverageExecutionTime",
                table: "ProcessingResults",
                column: "AverageExecutionTime");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingResults_AverageValue",
                table: "ProcessingResults",
                column: "AverageValue");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingResults_FirstOperationDate",
                table: "ProcessingResults",
                column: "FirstOperationDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingResults_UploadedFileId",
                table: "ProcessingResults",
                column: "UploadedFileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UploadedFiles_IdempotencyKey",
                table: "UploadedFiles",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UploadedFiles_NormalizedFileName",
                table: "UploadedFiles",
                column: "NormalizedFileName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Values_UploadedFileId_Date",
                table: "Values",
                columns: new[] { "UploadedFileId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessingResults");

            migrationBuilder.DropTable(
                name: "Values");

            migrationBuilder.DropTable(
                name: "UploadedFiles");
        }
    }
}
