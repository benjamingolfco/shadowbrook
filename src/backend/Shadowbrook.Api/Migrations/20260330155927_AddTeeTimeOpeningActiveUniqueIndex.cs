using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shadowbrook.Api.Migrations;

/// <inheritdoc />
public partial class AddTeeTimeOpeningActiveUniqueIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Cancel duplicate Open tee time openings before adding the unique index.
        // Keeps the most recently created row (highest Id) and sets older duplicates to Cancelled.
        migrationBuilder.Sql("""
            WITH Duplicates AS (
                SELECT Id,
                       ROW_NUMBER() OVER (PARTITION BY CourseId, TeeTime ORDER BY Id DESC) AS RowNum
                FROM [TeeTimeOpenings]
                WHERE [Status] = 'Open'
            )
            UPDATE t
            SET t.[Status] = 'Cancelled', t.[CancelledAt] = SYSUTCDATETIME()
            FROM [TeeTimeOpenings] t
            INNER JOIN Duplicates d ON t.Id = d.Id
            WHERE d.RowNum > 1;
            """);

        migrationBuilder.Sql("""
            CREATE UNIQUE NONCLUSTERED INDEX [IX_TeeTimeOpenings_CourseId_TeeTime_ActiveUnique]
            ON [TeeTimeOpenings] ([CourseId], [TeeTime])
            WHERE [Status] = 'Open';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_TeeTimeOpenings_CourseId_TeeTime_ActiveUnique",
            table: "TeeTimeOpenings");
    }
}
