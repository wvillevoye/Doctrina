using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doctrina.Modules.Lti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeepLinkSelections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeepLinkNonces",
                schema: "lti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nonce = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Expires = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeepLinkNonces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeepLinkSelections",
                schema: "lti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Expires = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeepLinkSelections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeepLinkNonces_Expires",
                schema: "lti",
                table: "DeepLinkNonces",
                column: "Expires");

            migrationBuilder.CreateIndex(
                name: "IX_DeepLinkNonces_ToolId_Nonce",
                schema: "lti",
                table: "DeepLinkNonces",
                columns: new[] { "ToolId", "Nonce" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeepLinkSelections_Expires",
                schema: "lti",
                table: "DeepLinkSelections",
                column: "Expires");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeepLinkNonces",
                schema: "lti");

            migrationBuilder.DropTable(
                name: "DeepLinkSelections",
                schema: "lti");
        }
    }
}
