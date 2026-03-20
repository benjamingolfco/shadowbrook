using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class AddCourseWaitlist : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "OrganizationName",
            table: "Tenants",
            type: "nvarchar(450)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(450)",
            oldCollation: "Latin1_General_CI_AS");

        migrationBuilder.CreateTable(
            name: "CourseWaitlists",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                ShortCode = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CourseWaitlists", x => x.Id);
                table.ForeignKey(
                    name: "FK_CourseWaitlists_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CourseWaitlists_CourseId_Date",
            table: "CourseWaitlists",
            columns: new[] { "CourseId", "Date" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CourseWaitlists_ShortCode_Date",
            table: "CourseWaitlists",
            columns: new[] { "ShortCode", "Date" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CourseWaitlists");

        migrationBuilder.AlterColumn<string>(
            name: "OrganizationName",
            table: "Tenants",
            type: "nvarchar(450)",
            nullable: false,
            collation: "Latin1_General_CI_AS",
            oldClrType: typeof(string),
            oldType: "nvarchar(450)");
    }
}
