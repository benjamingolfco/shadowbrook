using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class ChangeWindowColumnsToDateTime : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<DateTime>(
            name: "WindowStart",
            table: "GolferWaitlistEntries",
            type: "datetime2",
            nullable: false,
            oldClrType: typeof(TimeOnly),
            oldType: "time");

        migrationBuilder.AlterColumn<DateTime>(
            name: "WindowEnd",
            table: "GolferWaitlistEntries",
            type: "datetime2",
            nullable: false,
            oldClrType: typeof(TimeOnly),
            oldType: "time");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<TimeOnly>(
            name: "WindowStart",
            table: "GolferWaitlistEntries",
            type: "time",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime2");

        migrationBuilder.AlterColumn<TimeOnly>(
            name: "WindowEnd",
            table: "GolferWaitlistEntries",
            type: "time",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime2");
    }
}
