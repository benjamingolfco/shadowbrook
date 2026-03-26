using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Wolverine.Http;

namespace Shadowbrook.Api.Features.Waitlist.Endpoints;

public static class RemoveWaitlistEntryEndpoint
{
    [WolverineDelete("/courses/{courseId}/walkup-waitlist/entries/{entryId}")]
    public static async Task<IResult> RemoveEntry(
        Guid courseId,
        Guid entryId,
        [NotBody] ApplicationDbContext db,
        IGolferWaitlistEntryRepository entryRepo)
    {
        // Look up entry
        var entry = await entryRepo.GetByIdAsync(entryId);
        if (entry is null || entry.RemovedAt is not null)
        {
            return Results.NotFound(new { error = "Waitlist entry not found." });
        }

        // Verify entry belongs to a waitlist for this course (tenant isolation)
        var belongsToCourse = await db.CourseWaitlists
            .AnyAsync(w => w.Id == entry.CourseWaitlistId && w.CourseId == courseId);

        if (!belongsToCourse)
        {
            return Results.NotFound(new { error = "Waitlist entry not found." });
        }

        entry.Remove();
        return Results.NoContent();
    }
}
