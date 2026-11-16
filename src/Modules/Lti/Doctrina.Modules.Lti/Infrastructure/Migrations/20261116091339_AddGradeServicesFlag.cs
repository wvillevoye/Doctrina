using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doctrina.Modules.Lti.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGradeServicesFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GradeServices",
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
                name: "GradeServices",
                schema: "lti",
                table: "Tools");
        }
    }
}
