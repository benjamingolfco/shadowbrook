using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class UpdateAppUserRole : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CourseAssignments");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CourseAssignments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AssignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CourseAssignments", x => x.Id);
                table.ForeignKey(
                    name: "FK_CourseAssignments_AppUsers_AppUserId",
                    column: x => x.AppUserId,
                    principalTable: "AppUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CourseAssignments_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CourseAssignments_AppUserId_CourseId",
            table: "CourseAssignments",
            columns: new[] { "AppUserId", "CourseId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CourseAssignments_CourseId",
            table: "CourseAssignments",
            column: "CourseId");
    }
}
