using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doctrina.Modules.Lti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameNoncesToJwtNonces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeepLinkNonces",
                schema: "lti");

            migrationBuilder.CreateTable(
                name: "JwtNonces",
                schema: "lti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nonce = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Expires = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JwtNonces", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JwtNonces_Expires",
                schema: "lti",
                table: "JwtNonces",
                column: "Expires");

            migrationBuilder.CreateIndex(
                name: "IX_JwtNonces_ToolId_Purpose_Nonce",
                schema: "lti",
                table: "JwtNonces",
                columns: new[] { "ToolId", "Purpose", "Nonce" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JwtNonces",
                schema: "lti");

            migrationBuilder.CreateTable(
                name: "DeepLinkNonces",
                schema: "lti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Expires = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Nonce = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ToolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeepLinkNonces", x => x.Id);
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
        }
    }
}
