using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class AddClaimedSlotsToTeeTimeOpening : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TeeTimeOpeningClaimedSlots",
            columns: table => new
            {
                BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TeeTimeOpeningId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GolferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GroupSize = table.Column<int>(type: "int", nullable: false),
                ClaimedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TeeTimeOpeningClaimedSlots", x => new { x.TeeTimeOpeningId, x.BookingId });
                table.ForeignKey(
                    name: "FK_TeeTimeOpeningClaimedSlots_TeeTimeOpenings_TeeTimeOpeningId",
                    column: x => x.TeeTimeOpeningId,
                    principalTable: "TeeTimeOpenings",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TeeTimeOpeningClaimedSlots");
    }
}
