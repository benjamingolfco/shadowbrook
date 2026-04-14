using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Teeforce.Api.Migrations;

/// <inheritdoc />
public partial class AddRateScheduleInvalidReason : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "InvalidReason",
            table: "RateSchedules",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "InvalidReason",
            table: "RateSchedules");
    }
}
