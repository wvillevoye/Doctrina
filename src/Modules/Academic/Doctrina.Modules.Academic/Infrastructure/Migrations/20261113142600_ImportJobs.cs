using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doctrina.Modules.Academic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportJobs",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DryRun = table.Column<bool>(type: "bit", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StartedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Warnings = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UploadName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    UploadType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Upload = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    Created = table.Column<int>(type: "int", nullable: false),
                    Updated = table.Column<int>(type: "int", nullable: false),
                    Unchanged = table.Column<int>(type: "int", nullable: false),
                    Failed = table.Column<int>(type: "int", nullable: false),
                    RepeatOfId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportJobRows",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    File = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Line = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportJobRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportJobRows_ImportJobs_ImportJobId",
                        column: x => x.ImportJobId,
                        principalSchema: "academic",
                        principalTable: "ImportJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobRows_ImportJobId_File_Line",
                schema: "academic",
                table: "ImportJobRows",
                columns: new[] { "ImportJobId", "File", "Line" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobs_CreatedAt",
                schema: "academic",
                table: "ImportJobs",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportJobRows",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "ImportJobs",
                schema: "academic");
        }
    }
}
