using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class MoveDomainEntitiesToDomainProject : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_GolferWaitlistEntries_CourseWaitlistId_GolferPhone",
            table: "GolferWaitlistEntries");

        migrationBuilder.DropColumn(
            name: "GolferName",
            table: "GolferWaitlistEntries");

        migrationBuilder.DropColumn(
            name: "GolferPhone",
            table: "GolferWaitlistEntries");

        migrationBuilder.CreateIndex(
            name: "IX_GolferWaitlistEntries_CourseWaitlistId_GolferId",
            table: "GolferWaitlistEntries",
            columns: new[] { "CourseWaitlistId", "GolferId" },
            unique: true,
            filter: "[RemovedAt] IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_GolferWaitlistEntries_CourseWaitlistId_GolferId",
            table: "GolferWaitlistEntries");

        migrationBuilder.AddColumn<string>(
            name: "GolferName",
            table: "GolferWaitlistEntries",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "GolferPhone",
            table: "GolferWaitlistEntries",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_GolferWaitlistEntries_CourseWaitlistId_GolferPhone",
            table: "GolferWaitlistEntries",
            columns: new[] { "CourseWaitlistId", "GolferPhone" },
            filter: "[RemovedAt] IS NULL");
    }
}
