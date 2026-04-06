using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Teeforce.Api.Migrations;

/// <inheritdoc />
public partial class AddAppUserSoftDelete : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAt",
            table: "AppUsers",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsDeleted",
            table: "AppUsers",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DeletedAt",
            table: "AppUsers");

        migrationBuilder.DropColumn(
            name: "IsDeleted",
            table: "AppUsers");
    }
}
