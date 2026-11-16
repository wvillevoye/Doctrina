using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doctrina.Modules.Lti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeepLinkingUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeepLinkingUrl",
                schema: "lti",
                table: "Tools",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicKeysUrl",
                schema: "lti",
                table: "Tools",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeepLinkingUrl",
                schema: "lti",
                table: "Tools");

            migrationBuilder.DropColumn(
                name: "PublicKeysUrl",
                schema: "lti",
                table: "Tools");
        }
    }
}
