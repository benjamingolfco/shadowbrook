using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTeeTimeRequestRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaitlistRequestAcceptances");

            migrationBuilder.AddColumn<Guid>(
                name: "RowVersion",
                table: "WaitlistRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "WaitlistRequests");

            migrationBuilder.CreateTable(
                name: "WaitlistRequestAcceptances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GolferWaitlistEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaitlistOfferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaitlistRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistRequestAcceptances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistRequestAcceptances_WaitlistOfferId",
                table: "WaitlistRequestAcceptances",
                column: "WaitlistOfferId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistRequestAcceptances_WaitlistRequestId_GolferWaitlistEntryId",
                table: "WaitlistRequestAcceptances",
                columns: new[] { "WaitlistRequestId", "GolferWaitlistEntryId" },
                unique: true);
        }
    }
}
