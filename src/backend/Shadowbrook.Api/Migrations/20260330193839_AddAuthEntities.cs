using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class AddAuthEntities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Courses_Tenants_OrganizationId",
            table: "Courses");

        migrationBuilder.CreateTable(
            name: "Organizations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Organizations", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AppUsers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IdentityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LastLoginAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppUsers", x => x.Id);
                table.ForeignKey(
                    name: "FK_AppUsers_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CourseAssignments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AssignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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
            name: "IX_AppUsers_IdentityId",
            table: "AppUsers",
            column: "IdentityId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AppUsers_OrganizationId",
            table: "AppUsers",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_CourseAssignments_AppUserId_CourseId",
            table: "CourseAssignments",
            columns: new[] { "AppUserId", "CourseId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CourseAssignments_CourseId",
            table: "CourseAssignments",
            column: "CourseId");

        migrationBuilder.CreateIndex(
            name: "IX_Organizations_Name",
            table: "Organizations",
            column: "Name",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Courses_Organizations_OrganizationId",
            table: "Courses",
            column: "OrganizationId",
            principalTable: "Organizations",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Courses_Organizations_OrganizationId",
            table: "Courses");

        migrationBuilder.DropTable(
            name: "CourseAssignments");

        migrationBuilder.DropTable(
            name: "AppUsers");

        migrationBuilder.DropTable(
            name: "Organizations");

        migrationBuilder.AddForeignKey(
            name: "FK_Courses_Tenants_OrganizationId",
            table: "Courses",
            column: "OrganizationId",
            principalTable: "Tenants",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
