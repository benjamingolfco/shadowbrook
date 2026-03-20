using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class SeparateTeeTimeRequestAggregate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_WaitlistRequests_CourseWaitlists_CourseWaitlistId",
            table: "WaitlistRequests");

        migrationBuilder.DropIndex(
            name: "IX_WaitlistRequests_CourseWaitlistId_Status",
            table: "WaitlistRequests");

        migrationBuilder.DropIndex(
            name: "IX_WaitlistRequests_CourseWaitlistId_TeeTime",
            table: "WaitlistRequests");

        migrationBuilder.RenameColumn(
            name: "CourseWaitlistId",
            table: "WaitlistRequests",
            newName: "CourseId");

        migrationBuilder.AddColumn<DateOnly>(
            name: "Date",
            table: "WaitlistRequests",
            type: "date",
            nullable: false,
            defaultValue: new DateOnly(1, 1, 1));

        migrationBuilder.CreateIndex(
            name: "IX_WaitlistRequests_CourseId_Date_Status",
            table: "WaitlistRequests",
            columns: new[] { "CourseId", "Date", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_WaitlistRequests_CourseId_Date_TeeTime",
            table: "WaitlistRequests",
            columns: new[] { "CourseId", "Date", "TeeTime" });

        migrationBuilder.AddForeignKey(
            name: "FK_WaitlistRequests_Courses_CourseId",
            table: "WaitlistRequests",
            column: "CourseId",
            principalTable: "Courses",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_WaitlistRequests_Courses_CourseId",
            table: "WaitlistRequests");

        migrationBuilder.DropIndex(
            name: "IX_WaitlistRequests_CourseId_Date_Status",
            table: "WaitlistRequests");

        migrationBuilder.DropIndex(
            name: "IX_WaitlistRequests_CourseId_Date_TeeTime",
            table: "WaitlistRequests");

        migrationBuilder.DropColumn(
            name: "Date",
            table: "WaitlistRequests");

        migrationBuilder.RenameColumn(
            name: "CourseId",
            table: "WaitlistRequests",
            newName: "CourseWaitlistId");

        migrationBuilder.CreateIndex(
            name: "IX_WaitlistRequests_CourseWaitlistId_Status",
            table: "WaitlistRequests",
            columns: new[] { "CourseWaitlistId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_WaitlistRequests_CourseWaitlistId_TeeTime",
            table: "WaitlistRequests",
            columns: new[] { "CourseWaitlistId", "TeeTime" });

        migrationBuilder.AddForeignKey(
            name: "FK_WaitlistRequests_CourseWaitlists_CourseWaitlistId",
            table: "WaitlistRequests",
            column: "CourseWaitlistId",
            principalTable: "CourseWaitlists",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
