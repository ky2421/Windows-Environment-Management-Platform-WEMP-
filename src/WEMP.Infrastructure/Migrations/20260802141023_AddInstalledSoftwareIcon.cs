using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEMP.Migrations
{
    /// <inheritdoc />
    public partial class AddInstalledSoftwareIcon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IconPath",
                table: "installed_software",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IconPath",
                table: "installed_software");
        }
    }
}
