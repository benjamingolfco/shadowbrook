using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class RestoreAsReadOnlyOnDomainEvents : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "CurrentOfferId",
            table: "TeeTimeOfferPolicies",
            newName: "LastOfferId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "LastOfferId",
            table: "TeeTimeOfferPolicies",
            newName: "CurrentOfferId");
    }
}
