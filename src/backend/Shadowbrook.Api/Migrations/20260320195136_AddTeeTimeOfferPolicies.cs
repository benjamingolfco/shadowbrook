using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class AddTeeTimeOfferPolicies : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TeeTimeOfferPolicies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CurrentOfferId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsBuffering = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_TeeTimeOfferPolicies", x => x.Id));

        migrationBuilder.CreateTable(
            name: "TeeTimeRequestExpirationPolicies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_TeeTimeRequestExpirationPolicies", x => x.Id));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TeeTimeOfferPolicies");

        migrationBuilder.DropTable(
            name: "TeeTimeRequestExpirationPolicies");
    }
}
