using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doctrina.Modules.Lti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "lti");

            migrationBuilder.CreateTable(
                name: "Keys",
                schema: "lti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kid = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrivateKey = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    PublicKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    RetiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExternalId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DateLastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Keys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tools",
                schema: "lti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeploymentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LoginUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    LaunchUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    RedirectUris = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    SendName = table.Column<bool>(type: "bit", nullable: false),
                    SendEmail = table.Column<bool>(type: "bit", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DateLastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tools", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Keys_Active",
                schema: "lti",
                table: "Keys",
                column: "Active",
                unique: true,
                filter: "[Active] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Keys_Kid",
                schema: "lti",
                table: "Keys",
                column: "Kid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Keys_Source_ExternalId",
                schema: "lti",
                table: "Keys",
                columns: new[] { "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tools_ClientId",
                schema: "lti",
                table: "Tools",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tools_Source_ExternalId",
                schema: "lti",
                table: "Tools",
                columns: new[] { "Source", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Keys",
                schema: "lti");

            migrationBuilder.DropTable(
                name: "Tools",
                schema: "lti");
        }
    }
}
