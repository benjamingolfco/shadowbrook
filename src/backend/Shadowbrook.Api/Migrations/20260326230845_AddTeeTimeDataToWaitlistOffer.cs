using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class AddTeeTimeDataToWaitlistOffer : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CourseId",
            table: "WaitlistOffers",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<DateOnly>(
            name: "Date",
            table: "WaitlistOffers",
            type: "date",
            nullable: false,
            defaultValue: new DateOnly(1, 1, 1));

        migrationBuilder.AddColumn<TimeOnly>(
            name: "TeeTime",
            table: "WaitlistOffers",
            type: "time",
            nullable: false,
            defaultValue: new TimeOnly(0, 0, 0));

        migrationBuilder.CreateIndex(
            name: "IX_WaitlistOffers_CourseId",
            table: "WaitlistOffers",
            column: "CourseId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_WaitlistOffers_CourseId",
            table: "WaitlistOffers");

        migrationBuilder.DropColumn(
            name: "CourseId",
            table: "WaitlistOffers");

        migrationBuilder.DropColumn(
            name: "Date",
            table: "WaitlistOffers");

        migrationBuilder.DropColumn(
            name: "TeeTime",
            table: "WaitlistOffers");
    }
}
