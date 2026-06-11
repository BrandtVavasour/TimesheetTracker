using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimesheetTracker.DataModel.Migrations
{
    /// <inheritdoc />
    public partial class EntryWorkFromHome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsWorkFromHome",
                table: "TimeEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsWorkFromHome",
                table: "TimeEntries");
        }
    }
}
