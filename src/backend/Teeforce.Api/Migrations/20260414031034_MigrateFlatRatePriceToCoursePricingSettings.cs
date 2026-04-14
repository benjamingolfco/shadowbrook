using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Teeforce.Api.Migrations
{
    /// <inheritdoc />
    public partial class MigrateFlatRatePriceToCoursePricingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO CoursePricingSettings (Id, CourseId, DefaultPrice, MinPrice, MaxPrice, CreatedAt, UpdatedAt, UpdatedBy, RowVersion)
                SELECT
                    NEWID(),
                    Id,
                    FlatRatePrice,
                    NULL,
                    NULL,
                    GETUTCDATE(),
                    GETUTCDATE(),
                    NULL,
                    0x00000000
                FROM Courses
                WHERE Id NOT IN (SELECT CourseId FROM CoursePricingSettings)
            ");

            migrationBuilder.DropColumn(
                name: "FlatRatePrice",
                table: "Courses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FlatRatePrice",
                table: "Courses",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }
    }
}
