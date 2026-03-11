using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistOffersAndAcceptances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WaitlistOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeeTimeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GolferWaitlistEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GolferPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CourseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TeeTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    OfferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ResponseWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    OfferedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaitlistOffers_GolferWaitlistEntries_GolferWaitlistEntryId",
                        column: x => x.GolferWaitlistEntryId,
                        principalTable: "GolferWaitlistEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WaitlistRequestAcceptances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaitlistRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GolferWaitlistEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistRequestAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaitlistRequestAcceptances_GolferWaitlistEntries_GolferWaitlistEntryId",
                        column: x => x.GolferWaitlistEntryId,
                        principalTable: "GolferWaitlistEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WaitlistRequestAcceptances_WaitlistRequests_WaitlistRequestId",
                        column: x => x.WaitlistRequestId,
                        principalTable: "WaitlistRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistOffers_GolferPhone_Status",
                table: "WaitlistOffers",
                columns: new[] { "GolferPhone", "Status" },
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistOffers_GolferWaitlistEntryId",
                table: "WaitlistOffers",
                column: "GolferWaitlistEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistOffers_TeeTimeRequestId",
                table: "WaitlistOffers",
                column: "TeeTimeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistRequestAcceptances_GolferWaitlistEntryId",
                table: "WaitlistRequestAcceptances",
                column: "GolferWaitlistEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistRequestAcceptances_WaitlistRequestId",
                table: "WaitlistRequestAcceptances",
                column: "WaitlistRequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaitlistOffers");

            migrationBuilder.DropTable(
                name: "WaitlistRequestAcceptances");
        }
    }
}
