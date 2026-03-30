using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class AddTeeTimeOpeningActiveUniqueIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
            CREATE UNIQUE NONCLUSTERED INDEX [IX_TeeTimeOpenings_CourseId_TeeTime_ActiveUnique]
            ON [TeeTimeOpenings] ([CourseId], [TeeTime])
            WHERE [Status] = 'Open';
            """);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_TeeTimeOpenings_CourseId_TeeTime_ActiveUnique",
            table: "TeeTimeOpenings");
    }
}
