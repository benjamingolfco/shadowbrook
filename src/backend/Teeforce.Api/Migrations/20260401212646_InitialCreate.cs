using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Teeforce.Api.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Bookings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GolferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PlayerCount = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                TeeTime = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Bookings", x => x.Id));

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

        migrationBuilder.CreateTable(
            name: "Golfers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Golfers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Organizations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                FeatureFlags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Organizations", x => x.Id));

        migrationBuilder.CreateTable(
            name: "TeeTimeOpeningExpirationPolicies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_TeeTimeOpeningExpirationPolicies", x => x.Id));

        migrationBuilder.CreateTable(
            name: "TeeTimeOpeningOfferPolicies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PendingOfferCount = table.Column<int>(type: "int", nullable: false),
                SlotsRemaining = table.Column<int>(type: "int", nullable: false),
                GracePeriodExpired = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_TeeTimeOpeningOfferPolicies", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Tenants",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Tenants", x => x.Id));

        migrationBuilder.CreateTable(
            name: "WaitlistOfferResponsePolicies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OpeningId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_WaitlistOfferResponsePolicies", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AppUsers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IdentityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LastLoginAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppUsers", x => x.Id);
                table.ForeignKey(
                    name: "FK_AppUsers_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Courses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                StreetAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ZipCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                ContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                TeeTimeIntervalMinutes = table.Column<int>(type: "int", nullable: true),
                FirstTeeTime = table.Column<TimeOnly>(type: "time", nullable: true),
                LastTeeTime = table.Column<TimeOnly>(type: "time", nullable: true),
                FlatRatePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                FeatureFlags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                WaitlistEnabled = table.Column<bool>(type: "bit", nullable: true),
                TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Courses", x => x.Id);
                table.ForeignKey(
                    name: "FK_Courses_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "CourseWaitlists",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                WaitlistType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                ShortCode = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CourseWaitlists", x => x.Id);
                table.ForeignKey(
                    name: "FK_CourseWaitlists_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TeeTimeOpenings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SlotsAvailable = table.Column<int>(type: "int", nullable: false),
                SlotsRemaining = table.Column<int>(type: "int", nullable: false),
                OperatorOwned = table.Column<bool>(type: "bit", nullable: false),
                Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                FilledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ExpiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                TeeTime = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TeeTimeOpenings", x => x.Id);
                table.ForeignKey(
                    name: "FK_TeeTimeOpenings_Courses_CourseId",
                    column: x => x.CourseId,
                    principalTable: "Courses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "GolferWaitlistEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseWaitlistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GolferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IsWalkUp = table.Column<bool>(type: "bit", nullable: false),
                GroupSize = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                WindowStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                WindowEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                JoinedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RemovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GolferWaitlistEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_GolferWaitlistEntries_CourseWaitlists_CourseWaitlistId",
                    column: x => x.CourseWaitlistId,
                    principalTable: "CourseWaitlists",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_GolferWaitlistEntries_Golfers_GolferId",
                    column: x => x.GolferId,
                    principalTable: "Golfers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

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

        migrationBuilder.CreateTable(
            name: "WaitlistOffers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Token = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OpeningId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GolferWaitlistEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GolferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GroupSize = table.Column<int>(type: "int", nullable: false),
                IsWalkUp = table.Column<bool>(type: "bit", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                NotifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsStale = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                TeeTime = table.Column<TimeOnly>(type: "time", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WaitlistOffers", x => x.Id);
                table.ForeignKey(
                    name: "FK_WaitlistOffers_TeeTimeOpenings_OpeningId",
                    column: x => x.OpeningId,
                    principalTable: "TeeTimeOpenings",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AppUsers_IdentityId",
            table: "AppUsers",
            column: "IdentityId",
            unique: true,
            filter: "[IdentityId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_AppUsers_OrganizationId",
            table: "AppUsers",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Bookings_CourseId",
            table: "Bookings",
            column: "CourseId");

        migrationBuilder.CreateIndex(
            name: "IX_Courses_OrganizationId",
            table: "Courses",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Courses_OrganizationId_Name",
            table: "Courses",
            columns: new[] { "OrganizationId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CourseWaitlists_CourseId_Date",
            table: "CourseWaitlists",
            columns: new[] { "CourseId", "Date" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CourseWaitlists_ShortCode_Date",
            table: "CourseWaitlists",
            columns: new[] { "ShortCode", "Date" });

        migrationBuilder.CreateIndex(
            name: "IX_DevSmsMessages_Timestamp",
            table: "DevSmsMessages",
            column: "Timestamp");

        migrationBuilder.CreateIndex(
            name: "IX_Golfers_Phone",
            table: "Golfers",
            column: "Phone",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GolferWaitlistEntries_CourseWaitlistId_GolferId",
            table: "GolferWaitlistEntries",
            columns: new[] { "CourseWaitlistId", "GolferId" },
            unique: true,
            filter: "[RemovedAt] IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_GolferWaitlistEntries_GolferId",
            table: "GolferWaitlistEntries",
            column: "GolferId");

        migrationBuilder.CreateIndex(
            name: "IX_Organizations_Name",
            table: "Organizations",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TeeTimeOpenings_CourseId_Status",
            table: "TeeTimeOpenings",
            columns: new[] { "CourseId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_Tenants_OrganizationName",
            table: "Tenants",
            column: "OrganizationName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WaitlistOffers_CourseId",
            table: "WaitlistOffers",
            column: "CourseId");

        migrationBuilder.CreateIndex(
            name: "IX_WaitlistOffers_GolferWaitlistEntryId_OpeningId",
            table: "WaitlistOffers",
            columns: new[] { "GolferWaitlistEntryId", "OpeningId" });

        migrationBuilder.CreateIndex(
            name: "IX_WaitlistOffers_OpeningId",
            table: "WaitlistOffers",
            column: "OpeningId");

        migrationBuilder.CreateIndex(
            name: "IX_WaitlistOffers_Token",
            table: "WaitlistOffers",
            column: "Token",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AppUsers");

        migrationBuilder.DropTable(
            name: "Bookings");

        migrationBuilder.DropTable(
            name: "DevSmsMessages");

        migrationBuilder.DropTable(
            name: "GolferWaitlistEntries");

        migrationBuilder.DropTable(
            name: "TeeTimeOpeningClaimedSlots");

        migrationBuilder.DropTable(
            name: "TeeTimeOpeningExpirationPolicies");

        migrationBuilder.DropTable(
            name: "TeeTimeOpeningOfferPolicies");

        migrationBuilder.DropTable(
            name: "Tenants");

        migrationBuilder.DropTable(
            name: "WaitlistOfferResponsePolicies");

        migrationBuilder.DropTable(
            name: "WaitlistOffers");

        migrationBuilder.DropTable(
            name: "CourseWaitlists");

        migrationBuilder.DropTable(
            name: "Golfers");

        migrationBuilder.DropTable(
            name: "TeeTimeOpenings");

        migrationBuilder.DropTable(
            name: "Courses");

        migrationBuilder.DropTable(
            name: "Organizations");
    }
}
