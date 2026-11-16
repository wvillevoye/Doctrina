using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doctrina.Modules.Lti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLineItemsAndScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LineItems",
                schema: "lti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ScoreMaximum = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Tag = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StartDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExternalId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DateLastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Scores",
                schema: "lti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoreGiven = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ScoreMaximum = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActivityProgress = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GradingProgress = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scores_LineItems_LineItemId",
                        column: x => x.LineItemId,
                        principalSchema: "lti",
                        principalTable: "LineItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LineItems_Source_ExternalId",
                schema: "lti",
                table: "LineItems",
                columns: new[] { "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LineItems_ToolId_OfferingId",
                schema: "lti",
                table: "LineItems",
                columns: new[] { "ToolId", "OfferingId" });

            migrationBuilder.CreateIndex(
                name: "IX_Scores_LineItemId_PersonId",
                schema: "lti",
                table: "Scores",
                columns: new[] { "LineItemId", "PersonId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Scores",
                schema: "lti");

            migrationBuilder.DropTable(
                name: "LineItems",
                schema: "lti");
        }
    }
}
