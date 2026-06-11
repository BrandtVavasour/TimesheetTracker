using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimesheetTracker.DataModel.Migrations
{
    /// <inheritdoc />
    public partial class JobDefaultHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "DefaultEndTime",
                table: "Jobs",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "DefaultStartTime",
                table: "Jobs",
                type: "time without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultEndTime",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "DefaultStartTime",
                table: "Jobs");
        }
    }
}
