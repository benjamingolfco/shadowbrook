using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Teeforce.Api.Migrations;

/// <inheritdoc />
public partial class AddCoursePricingSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "Price",
            table: "TeeTimeClaims",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Price",
            table: "TeeSheetIntervals",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "RateScheduleId",
            table: "TeeSheetIntervals",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "PricePerPlayer",
            table: "Bookings",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "TotalPrice",
            table: "Bookings",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "CoursePricingSettings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DefaultPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                MinPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                MaxPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CoursePricingSettings", x => x.Id);
                table.ForeignKey(
                    name: "FK_CoursePricingSettings_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RateSchedules",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CoursePricingSettingsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                DaysOfWeek = table.Column<string>(type: "nvarchar(200)", nullable: false),
                StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RateSchedules", x => x.Id);
                table.ForeignKey(
                    name: "FK_RateSchedules_CoursePricingSettings_CoursePricingSettingsId",
                    column: x => x.CoursePricingSettingsId,
                    principalTable: "CoursePricingSettings",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CoursePricingSettings_CourseId",
            table: "CoursePricingSettings",
            column: "CourseId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RateSchedules_CoursePricingSettingsId",
            table: "RateSchedules",
            column: "CoursePricingSettingsId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RateSchedules");

        migrationBuilder.DropTable(
            name: "CoursePricingSettings");

        migrationBuilder.DropColumn(
            name: "Price",
            table: "TeeTimeClaims");

        migrationBuilder.DropColumn(
            name: "Price",
            table: "TeeSheetIntervals");

        migrationBuilder.DropColumn(
            name: "RateScheduleId",
            table: "TeeSheetIntervals");

        migrationBuilder.DropColumn(
            name: "PricePerPlayer",
            table: "Bookings");

        migrationBuilder.DropColumn(
            name: "TotalPrice",
            table: "Bookings");
    }
}
