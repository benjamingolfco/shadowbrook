using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class AddGolferWaitlistEntryHierarchy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsReady",
            table: "GolferWaitlistEntries");

        migrationBuilder.AddColumn<TimeOnly>(
            name: "WindowEnd",
            table: "GolferWaitlistEntries",
            type: "time",
            nullable: false,
            defaultValue: new TimeOnly(0, 0, 0));

        migrationBuilder.AddColumn<TimeOnly>(
            name: "WindowStart",
            table: "GolferWaitlistEntries",
            type: "time",
            nullable: false,
            defaultValue: new TimeOnly(0, 0, 0));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "WindowEnd",
            table: "GolferWaitlistEntries");

        migrationBuilder.DropColumn(
            name: "WindowStart",
            table: "GolferWaitlistEntries");

        migrationBuilder.AddColumn<bool>(
            name: "IsReady",
            table: "GolferWaitlistEntries",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }
}
