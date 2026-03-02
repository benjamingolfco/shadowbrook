using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGolferAndWaitlistEntry : Migration
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
                name: "Golfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Golfers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WalkUpCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalkUpCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalkUpCodes_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GolferWaitlistEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseWaitlistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GolferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GolferName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GolferPhone = table.Column<string>(type: "nvarchar(450)", nullable: false),
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
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseWaitlists_CourseId_Date",
                table: "CourseWaitlists",
                columns: new[] { "CourseId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Golfers_Phone",
                table: "Golfers",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GolferWaitlistEntries_CourseWaitlistId_GolferId",
                table: "GolferWaitlistEntries",
                columns: new[] { "CourseWaitlistId", "GolferId" });

            migrationBuilder.CreateIndex(
                name: "IX_GolferWaitlistEntries_CourseWaitlistId_GolferPhone",
                table: "GolferWaitlistEntries",
                columns: new[] { "CourseWaitlistId", "GolferPhone" });

            migrationBuilder.CreateIndex(
                name: "IX_GolferWaitlistEntries_CourseWaitlistId_IsWalkUp_IsReady",
                table: "GolferWaitlistEntries",
                columns: new[] { "CourseWaitlistId", "IsWalkUp", "IsReady" });

            migrationBuilder.CreateIndex(
                name: "IX_GolferWaitlistEntries_GolferId",
                table: "GolferWaitlistEntries",
                column: "GolferId");

            migrationBuilder.CreateIndex(
                name: "IX_WalkUpCodes_Code_Date",
                table: "WalkUpCodes",
                columns: new[] { "Code", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalkUpCodes_CourseId_Date",
                table: "WalkUpCodes",
                columns: new[] { "CourseId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GolferWaitlistEntries");

            migrationBuilder.DropTable(
                name: "WalkUpCodes");

            migrationBuilder.DropTable(
                name: "CourseWaitlists");

            migrationBuilder.DropTable(
                name: "Golfers");

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
