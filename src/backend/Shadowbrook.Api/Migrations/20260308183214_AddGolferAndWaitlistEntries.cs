using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class AddGolferAndWaitlistEntries : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Golfers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Golfers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "GolferWaitlistEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseWaitlistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GolferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GolferName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                GolferPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                IsWalkUp = table.Column<bool>(type: "bit", nullable: false),
                IsReady = table.Column<bool>(type: "bit", nullable: false),
                JoinedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RemovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GolferWaitlistEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_GolferWaitlistEntries_CourseWaitlists_CourseWaitlistId",
                    column: x => x.CourseWaitlistId,
                    principalTable: "CourseWaitlists",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_GolferWaitlistEntries_Golfers_GolferId",
                    column: x => x.GolferId,
                    principalTable: "Golfers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Golfers_Phone",
            table: "Golfers",
            column: "Phone",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GolferWaitlistEntries_CourseWaitlistId_GolferPhone",
            table: "GolferWaitlistEntries",
            columns: new[] { "CourseWaitlistId", "GolferPhone" },
            filter: "[RemovedAt] IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_GolferWaitlistEntries_GolferId",
            table: "GolferWaitlistEntries",
            column: "GolferId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "GolferWaitlistEntries");

        migrationBuilder.DropTable(
            name: "Golfers");
    }
}
