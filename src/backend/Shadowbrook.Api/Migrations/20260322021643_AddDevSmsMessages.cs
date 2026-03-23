using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class AddDevSmsMessages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DevSmsMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                From = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                To = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Direction = table.Column<int>(type: "int", nullable: false),
                Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DevSmsMessages", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_DevSmsMessages_Timestamp",
            table: "DevSmsMessages",
            column: "Timestamp");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DevSmsMessages");
    }
}
