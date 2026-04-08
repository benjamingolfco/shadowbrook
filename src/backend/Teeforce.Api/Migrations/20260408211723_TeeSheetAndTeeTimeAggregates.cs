using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Teeforce.Api.Migrations;

/// <inheritdoc />
public partial class TeeSheetAndTeeTimeAggregates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DefaultCapacity",
            table: "Courses",
            type: "int",
            nullable: false,
            defaultValue: 4);

        migrationBuilder.AddColumn<Guid>(
            name: "TeeTimeId",
            table: "Bookings",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "TeeSheets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Settings_DefaultCapacity = table.Column<int>(type: "int", nullable: false),
                Settings_FirstTeeTime = table.Column<TimeOnly>(type: "time", nullable: false),
                Settings_IntervalMinutes = table.Column<int>(type: "int", nullable: false),
                Settings_LastTeeTime = table.Column<TimeOnly>(type: "time", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TeeSheets", x => x.Id);
                table.ForeignKey(
                    name: "FK_TeeSheets_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TeeTimes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TeeSheetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TeeSheetIntervalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Time = table.Column<TimeOnly>(type: "time", nullable: false),
                Capacity = table.Column<int>(type: "int", nullable: false),
                Remaining = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TeeTimes", x => x.Id);
                table.ForeignKey(
                    name: "FK_TeeTimes_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TeeSheetIntervals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TeeSheetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Time = table.Column<TimeOnly>(type: "time", nullable: false),
                Capacity = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TeeSheetIntervals", x => x.Id);
                table.ForeignKey(
                    name: "FK_TeeSheetIntervals_TeeSheets_TeeSheetId",
                    column: x => x.TeeSheetId,
                    principalTable: "TeeSheets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TeeTimeClaims",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TeeTimeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GolferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GroupSize = table.Column<int>(type: "int", nullable: false),
                ClaimedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TeeTimeClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_TeeTimeClaims_TeeTimes_TeeTimeId",
                    column: x => x.TeeTimeId,
                    principalTable: "TeeTimes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Bookings_TeeTimeId",
            table: "Bookings",
            column: "TeeTimeId");

        migrationBuilder.CreateIndex(
            name: "IX_TeeSheetIntervals_TeeSheetId_Time",
            table: "TeeSheetIntervals",
            columns: new[] { "TeeSheetId", "Time" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TeeSheets_CourseId_Date",
            table: "TeeSheets",
            columns: new[] { "CourseId", "Date" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TeeTimeClaims_TeeTimeId_BookingId",
            table: "TeeTimeClaims",
            columns: new[] { "TeeTimeId", "BookingId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TeeTimes_CourseId_Date",
            table: "TeeTimes",
            columns: new[] { "CourseId", "Date" });

        migrationBuilder.CreateIndex(
            name: "IX_TeeTimes_TeeSheetIntervalId",
            table: "TeeTimes",
            column: "TeeSheetIntervalId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TeeSheetIntervals");

        migrationBuilder.DropTable(
            name: "TeeTimeClaims");

        migrationBuilder.DropTable(
            name: "TeeSheets");

        migrationBuilder.DropTable(
            name: "TeeTimes");

        migrationBuilder.DropIndex(
            name: "IX_Bookings_TeeTimeId",
            table: "Bookings");

        migrationBuilder.DropColumn(
            name: "DefaultCapacity",
            table: "Courses");

        migrationBuilder.DropColumn(
            name: "TeeTimeId",
            table: "Bookings");
    }
}
