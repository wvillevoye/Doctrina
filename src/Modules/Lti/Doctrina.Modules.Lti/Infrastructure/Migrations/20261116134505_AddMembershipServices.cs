using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doctrina.Modules.Lti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MembershipServices",
                schema: "lti",
                table: "Tools",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MembershipServices",
                schema: "lti",
                table: "Tools");
        }
    }
}
