using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Teeforce.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTeeTimeOfferAndAvailabilityPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeeTimeAvailabilityPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PendingOfferIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SlotsRemaining = table.Column<int>(type: "int", nullable: false),
                    GracePeriodExpired = table.Column<bool>(type: "bit", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeeTimeAvailabilityPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeeTimeOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeeTimeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GolferWaitlistEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GolferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupSize = table.Column<int>(type: "int", nullable: false),
                    Token = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsStale = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NotifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeeTimeOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeeTimeOffers_TeeTimes_TeeTimeId",
                        column: x => x.TeeTimeId,
                        principalTable: "TeeTimes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeeTimeOffers_CourseId",
                table: "TeeTimeOffers",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_TeeTimeOffers_TeeTimeId",
                table: "TeeTimeOffers",
                column: "TeeTimeId");

            migrationBuilder.CreateIndex(
                name: "IX_TeeTimeOffers_TeeTimeId_GolferId_Status",
                table: "TeeTimeOffers",
                columns: new[] { "TeeTimeId", "GolferId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TeeTimeOffers_Token",
                table: "TeeTimeOffers",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeeTimeAvailabilityPolicies");

            migrationBuilder.DropTable(
                name: "TeeTimeOffers");
        }
    }
}
