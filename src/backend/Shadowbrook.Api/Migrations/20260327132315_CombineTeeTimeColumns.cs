using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class CombineTeeTimeColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // TeeTimeOpenings: combine Date + TeeTime (time) into a single datetime2 column
        migrationBuilder.AddColumn<DateTime>(
            name: "TeeTime_New",
            table: "TeeTimeOpenings",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1));

        migrationBuilder.Sql(
            "UPDATE [TeeTimeOpenings] SET [TeeTime_New] = DATEADD(day, DATEDIFF(day, 0, [Date]), CAST([TeeTime] AS datetime2))");

        migrationBuilder.DropColumn(name: "Date", table: "TeeTimeOpenings");
        migrationBuilder.DropColumn(name: "TeeTime", table: "TeeTimeOpenings");
        migrationBuilder.RenameColumn(name: "TeeTime_New", table: "TeeTimeOpenings", newName: "TeeTime");

        // Bookings: combine Date + TeeTime (time) into a single datetime2 column
        migrationBuilder.AddColumn<DateTime>(
            name: "TeeTime_New",
            table: "Bookings",
            type: "datetime2",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1));

        migrationBuilder.Sql(
            "UPDATE [Bookings] SET [TeeTime_New] = DATEADD(day, DATEDIFF(day, 0, [Date]), CAST([TeeTime] AS datetime2))");

        migrationBuilder.DropColumn(name: "Date", table: "Bookings");
        migrationBuilder.DropColumn(name: "TeeTime", table: "Bookings");
        migrationBuilder.RenameColumn(name: "TeeTime_New", table: "Bookings", newName: "TeeTime");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // TeeTimeOpenings: split datetime2 back into Date + TeeTime (time)
        migrationBuilder.AddColumn<DateOnly>(
            name: "Date",
            table: "TeeTimeOpenings",
            type: "date",
            nullable: false,
            defaultValue: new DateOnly(1, 1, 1));

        migrationBuilder.Sql(
            "UPDATE [TeeTimeOpenings] SET [Date] = CAST([TeeTime] AS date)");

        migrationBuilder.AddColumn<TimeOnly>(
            name: "TeeTime_Old",
            table: "TeeTimeOpenings",
            type: "time",
            nullable: false,
            defaultValue: new TimeOnly(0, 0));

        migrationBuilder.Sql(
            "UPDATE [TeeTimeOpenings] SET [TeeTime_Old] = CAST([TeeTime] AS time)");

        migrationBuilder.DropColumn(name: "TeeTime", table: "TeeTimeOpenings");
        migrationBuilder.RenameColumn(name: "TeeTime_Old", table: "TeeTimeOpenings", newName: "TeeTime");

        // Bookings: split datetime2 back into Date + TeeTime (time)
        migrationBuilder.AddColumn<DateOnly>(
            name: "Date",
            table: "Bookings",
            type: "date",
            nullable: false,
            defaultValue: new DateOnly(1, 1, 1));

        migrationBuilder.Sql(
            "UPDATE [Bookings] SET [Date] = CAST([TeeTime] AS date)");

        migrationBuilder.AddColumn<TimeOnly>(
            name: "TeeTime_Old",
            table: "Bookings",
            type: "time",
            nullable: false,
            defaultValue: new TimeOnly(0, 0));

        migrationBuilder.Sql(
            "UPDATE [Bookings] SET [TeeTime_Old] = CAST([TeeTime] AS time)");

        migrationBuilder.DropColumn(name: "TeeTime", table: "Bookings");
        migrationBuilder.RenameColumn(name: "TeeTime_Old", table: "Bookings", newName: "TeeTime");
    }
}
