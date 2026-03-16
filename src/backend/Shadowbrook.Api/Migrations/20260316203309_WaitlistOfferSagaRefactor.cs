using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations
{
    /// <inheritdoc />
    public partial class WaitlistOfferSagaRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Courses_CourseId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CourseName",
                table: "WaitlistOffers");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "WaitlistOffers");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "WaitlistOffers");

            migrationBuilder.DropColumn(
                name: "GolferName",
                table: "WaitlistOffers");

            migrationBuilder.DropColumn(
                name: "GolferPhone",
                table: "WaitlistOffers");

            migrationBuilder.DropColumn(
                name: "GolfersNeeded",
                table: "WaitlistOffers");

            migrationBuilder.DropColumn(
                name: "TeeTime",
                table: "WaitlistOffers");

            migrationBuilder.RenameColumn(
                name: "CourseId",
                table: "WaitlistOffers",
                newName: "BookingId");

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "WaitlistOffers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GolferName",
                table: "Bookings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "GolferId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "WaitlistSlotFills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeeTimeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GolferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupSize = table.Column<int>(type: "int", nullable: false),
                    FilledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistSlotFills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaitlistSlotFills_WaitlistRequests_TeeTimeRequestId",
                        column: x => x.TeeTimeRequestId,
                        principalTable: "WaitlistRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistOffers_BookingId",
                table: "WaitlistOffers",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistSlotFills_BookingId",
                table: "WaitlistSlotFills",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistSlotFills_TeeTimeRequestId",
                table: "WaitlistSlotFills",
                column: "TeeTimeRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaitlistSlotFills");

            migrationBuilder.DropIndex(
                name: "IX_WaitlistOffers_BookingId",
                table: "WaitlistOffers");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "WaitlistOffers");

            migrationBuilder.DropColumn(
                name: "GolferId",
                table: "Bookings");

            migrationBuilder.RenameColumn(
                name: "BookingId",
                table: "WaitlistOffers",
                newName: "CourseId");

            migrationBuilder.AddColumn<string>(
                name: "CourseName",
                table: "WaitlistOffers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "Date",
                table: "WaitlistOffers",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "WaitlistOffers",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "GolferName",
                table: "WaitlistOffers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GolferPhone",
                table: "WaitlistOffers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "GolfersNeeded",
                table: "WaitlistOffers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "TeeTime",
                table: "WaitlistOffers",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AlterColumn<string>(
                name: "GolferName",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Courses_CourseId",
                table: "Bookings",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
