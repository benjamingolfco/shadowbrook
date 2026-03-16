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
                    Token = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeeTimeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GolferWaitlistEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TeeTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    GolfersNeeded = table.Column<int>(type: "int", nullable: false),
                    GolferName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GolferPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistOffers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaitlistRequestAcceptances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaitlistRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GolferWaitlistEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaitlistOfferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistRequestAcceptances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistOffers_GolferWaitlistEntryId_TeeTimeRequestId",
                table: "WaitlistOffers",
                columns: new[] { "GolferWaitlistEntryId", "TeeTimeRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistOffers_TeeTimeRequestId",
                table: "WaitlistOffers",
                column: "TeeTimeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistOffers_Token",
                table: "WaitlistOffers",
                column: "Token",
                unique: true);

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
