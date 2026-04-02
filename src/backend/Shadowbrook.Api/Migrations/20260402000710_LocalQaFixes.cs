using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class LocalQaFixes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // No-op: InitialCreate (squashed migration) already reflects the final schema.
        // This migration was created to upgrade an older schema, but is redundant
        // against a fresh database created from InitialCreate.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No-op: see Up().
    }
}
