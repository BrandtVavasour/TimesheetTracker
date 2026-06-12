using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimesheetTracker.DataModel.Migrations
{
    /// <inheritdoc />
    public partial class ExportIncludeAllDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExportIncludeAllDays",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExportIncludeAllDays",
                table: "Users");
        }
    }
}
