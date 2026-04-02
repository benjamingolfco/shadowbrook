using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class LocalQaFixes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AppUsers_IdentityId",
            table: "AppUsers");

        migrationBuilder.DropColumn(
            name: "DisplayName",
            table: "AppUsers");

        migrationBuilder.AlterColumn<string>(
            name: "IdentityId",
            table: "AppUsers",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100);

        migrationBuilder.AddColumn<string>(
            name: "FirstName",
            table: "AppUsers",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LastName",
            table: "AppUsers",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AppUsers_IdentityId",
            table: "AppUsers",
            column: "IdentityId",
            unique: true,
            filter: "[IdentityId] IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AppUsers_IdentityId",
            table: "AppUsers");

        migrationBuilder.DropColumn(
            name: "FirstName",
            table: "AppUsers");

        migrationBuilder.DropColumn(
            name: "LastName",
            table: "AppUsers");

        migrationBuilder.AlterColumn<string>(
            name: "IdentityId",
            table: "AppUsers",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100,
            oldNullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DisplayName",
            table: "AppUsers",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_AppUsers_IdentityId",
            table: "AppUsers",
            column: "IdentityId",
            unique: true);
    }
}
