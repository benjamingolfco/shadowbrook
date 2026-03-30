using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class RenameCourseOrganizationId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Courses_Tenants_TenantId",
            table: "Courses");

        migrationBuilder.RenameColumn(
            name: "TenantId",
            table: "Courses",
            newName: "OrganizationId");

        migrationBuilder.RenameIndex(
            name: "IX_Courses_TenantId_Name",
            table: "Courses",
            newName: "IX_Courses_OrganizationId_Name");

        migrationBuilder.RenameIndex(
            name: "IX_Courses_TenantId",
            table: "Courses",
            newName: "IX_Courses_OrganizationId");

        migrationBuilder.AddForeignKey(
            name: "FK_Courses_Tenants_OrganizationId",
            table: "Courses",
            column: "OrganizationId",
            principalTable: "Tenants",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Courses_Tenants_OrganizationId",
            table: "Courses");

        migrationBuilder.RenameColumn(
            name: "OrganizationId",
            table: "Courses",
            newName: "TenantId");

        migrationBuilder.RenameIndex(
            name: "IX_Courses_OrganizationId_Name",
            table: "Courses",
            newName: "IX_Courses_TenantId_Name");

        migrationBuilder.RenameIndex(
            name: "IX_Courses_OrganizationId",
            table: "Courses",
            newName: "IX_Courses_TenantId");

        migrationBuilder.AddForeignKey(
            name: "FK_Courses_Tenants_TenantId",
            table: "Courses",
            column: "TenantId",
            principalTable: "Tenants",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
