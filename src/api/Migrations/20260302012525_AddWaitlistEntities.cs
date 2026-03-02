using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistEntities : Migration
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

            migrationBuilder.AddColumn<bool>(
                name: "WaitlistEnabled",
                table: "Courses",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CourseWaitlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "WaitlistRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseWaitlistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeeTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    GolfersNeeded = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaitlistRequests_CourseWaitlists_CourseWaitlistId",
                        column: x => x.CourseWaitlistId,
                        principalTable: "CourseWaitlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseWaitlists_CourseId_Date",
                table: "CourseWaitlists",
                columns: new[] { "CourseId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistRequests_CourseWaitlistId_Status",
                table: "WaitlistRequests",
                columns: new[] { "CourseWaitlistId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistRequests_CourseWaitlistId_TeeTime",
                table: "WaitlistRequests",
                columns: new[] { "CourseWaitlistId", "TeeTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaitlistRequests");

            migrationBuilder.DropTable(
                name: "CourseWaitlists");

            migrationBuilder.DropColumn(
                name: "WaitlistEnabled",
                table: "Courses");

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
}
