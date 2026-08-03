using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WEMP.Migrations
{
    /// <inheritdoc />
    public partial class AddOptimizationRiskLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RiskLevel",
                table: "optimization_items",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RiskLevel",
                table: "optimization_items");
        }
    }
}
