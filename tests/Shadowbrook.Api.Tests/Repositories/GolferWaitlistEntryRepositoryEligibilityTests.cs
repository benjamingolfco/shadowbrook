using Microsoft.Extensions.DependencyInjection;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Repositories;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;

namespace Shadowbrook.Api.Tests.Repositories;

[Collection("Integration")]
[IntegrationTest]
public class GolferWaitlistEntryRepositoryEligibilityTests(TestWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly TestWebApplicationFactory factory = factory;

    public Task InitializeAsync() => this.factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // Normal Window (No Midnight Wrap)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FindEligibleEntries_NormalWindow_TeeTimeInWindow_ReturnsEntry()
    {
        // Window: 10:00-10:30, Tee time: 10:15
        var (courseId, date, entryId) = await CreateEntryAsync(
            windowStart: new TimeOnly(10, 0),
            windowEnd: new TimeOnly(10, 30));

        var entries = await FindEligibleAsync(courseId, date, new TimeOnly(10, 15));

        Assert.Single(entries);
        Assert.Equal(entryId, entries[0].Id);
    }

    [Fact]
    public async Task FindEligibleEntries_NormalWindow_TeeTimeAtStart_ReturnsEntry()
    {
        // Window: 10:00-10:30, Tee time: 10:00 (boundary)
        var (courseId, date, entryId) = await CreateEntryAsync(
            windowStart: new TimeOnly(10, 0),
            windowEnd: new TimeOnly(10, 30));

        var entries = await FindEligibleAsync(courseId, date, new TimeOnly(10, 0));

        Assert.Single(entries);
        Assert.Equal(entryId, entries[0].Id);
    }

    [Fact]
    public async Task FindEligibleEntries_NormalWindow_TeeTimeAtEnd_ReturnsEntry()
    {
        // Window: 10:00-10:30, Tee time: 10:30 (boundary)
        var (courseId, date, entryId) = await CreateEntryAsync(
            windowStart: new TimeOnly(10, 0),
            windowEnd: new TimeOnly(10, 30));

        var entries = await FindEligibleAsync(courseId, date, new TimeOnly(10, 30));

        Assert.Single(entries);
        Assert.Equal(entryId, entries[0].Id);
    }

    [Fact]
    public async Task FindEligibleEntries_NormalWindow_TeeTimeBeforeWindow_ReturnsEmpty()
    {
        // Window: 10:00-10:30, Tee time: 09:59
        var (courseId, date, _) = await CreateEntryAsync(
            windowStart: new TimeOnly(10, 0),
            windowEnd: new TimeOnly(10, 30));

        var entries = await FindEligibleAsync(courseId, date, new TimeOnly(9, 59));

        Assert.Empty(entries);
    }

    [Fact]
    public async Task FindEligibleEntries_NormalWindow_TeeTimeAfterWindow_ReturnsEmpty()
    {
        // Window: 10:00-10:30, Tee time: 10:31
        var (courseId, date, _) = await CreateEntryAsync(
            windowStart: new TimeOnly(10, 0),
            windowEnd: new TimeOnly(10, 30));

        var entries = await FindEligibleAsync(courseId, date, new TimeOnly(10, 31));

        Assert.Empty(entries);
    }

    // -------------------------------------------------------------------------
    // Midnight Wrap-Around Window
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FindEligibleEntries_MidnightWrap_TeeTimeInLateNightPortion_ReturnsEntry()
    {
        // Window: 23:45-00:15 (wraps midnight), Tee time: 23:50
        var (courseId, date, entryId) = await CreateEntryAsync(
            windowStart: new TimeOnly(23, 45),
            windowEnd: new TimeOnly(0, 15));

        var entries = await FindEligibleAsync(courseId, date, new TimeOnly(23, 50));

        Assert.Single(entries);
        Assert.Equal(entryId, entries[0].Id);
    }

    [Fact]
    public async Task FindEligibleEntries_MidnightWrap_TeeTimeInEarlyMorningPortion_ReturnsEntry()
    {
        // Window: 23:45-00:15 (wraps midnight), Tee time: 00:10
        var (courseId, date, entryId) = await CreateEntryAsync(
            windowStart: new TimeOnly(23, 45),
            windowEnd: new TimeOnly(0, 15));

        var entries = await FindEligibleAsync(courseId, date, new TimeOnly(0, 10));

        Assert.Single(entries);
        Assert.Equal(entryId, entries[0].Id);
    }

    [Fact]
    public async Task FindEligibleEntries_MidnightWrap_TeeTimeAtMidnight_ReturnsEntry()
    {
        // Window: 23:45-00:15 (wraps midnight), Tee time: 00:00
        var (courseId, date, entryId) = await CreateEntryAsync(
            windowStart: new TimeOnly(23, 45),
            windowEnd: new TimeOnly(0, 15));

        var entries = await FindEligibleAsync(courseId, date, new TimeOnly(0, 0));

        Assert.Single(entries);
        Assert.Equal(entryId, entries[0].Id);
    }

    [Fact]
    public async Task FindEligibleEntries_MidnightWrap_TeeTimeInGap_ReturnsEmpty()
    {
        // Window: 23:45-00:15 (wraps midnight), Tee time: 12:00 (in the gap)
        var (courseId, date, _) = await CreateEntryAsync(
            windowStart: new TimeOnly(23, 45),
            windowEnd: new TimeOnly(0, 15));

        var entries = await FindEligibleAsync(courseId, date, new TimeOnly(12, 0));

        Assert.Empty(entries);
    }

    [Fact]
    public async Task FindEligibleEntries_MidnightWrap_TeeTimeJustBeforeWindow_ReturnsEmpty()
    {
        // Window: 23:45-00:15 (wraps midnight), Tee time: 23:44
        var (courseId, date, _) = await CreateEntryAsync(
            windowStart: new TimeOnly(23, 45),
            windowEnd: new TimeOnly(0, 15));

        var entries = await FindEligibleAsync(courseId, date, new TimeOnly(23, 44));

        Assert.Empty(entries);
    }

    [Fact]
    public async Task FindEligibleEntries_MidnightWrap_TeeTimeJustAfterWindow_ReturnsEmpty()
    {
        // Window: 23:45-00:15 (wraps midnight), Tee time: 00:16
        var (courseId, date, _) = await CreateEntryAsync(
            windowStart: new TimeOnly(23, 45),
            windowEnd: new TimeOnly(0, 15));

        var entries = await FindEligibleAsync(courseId, date, new TimeOnly(0, 16));

        Assert.Empty(entries);
    }

    // -------------------------------------------------------------------------
    // Other Filter Checks (ensure we respect all filters)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FindEligibleEntries_RemovedEntry_ExcludedFromResults()
    {
        var (courseId, date, entryId) = await CreateEntryAsync(
            windowStart: new TimeOnly(10, 0),
            windowEnd: new TimeOnly(10, 30));

        // Remove the entry
        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var entry = await db.GolferWaitlistEntries.FindAsync(entryId);
            entry!.Remove(new TestTimeProvider());
            await db.SaveChangesAsync();
        }

        var entries = await FindEligibleAsync(courseId, date, new TimeOnly(10, 15));

        Assert.Empty(entries);
    }

    [Fact]
    public async Task FindEligibleEntries_GroupSizeExceedsMax_ExcludedFromResults()
    {
        // Entry has group size 4, max is 3
        var (courseId, date, _) = await CreateEntryAsync(
            windowStart: new TimeOnly(10, 0),
            windowEnd: new TimeOnly(10, 30),
            groupSize: 4);

        var entries = await FindEligibleAsync(courseId, date, new TimeOnly(10, 15), maxGroupSize: 3);

        Assert.Empty(entries);
    }

    [Fact]
    public async Task FindEligibleEntries_MultipleEntries_OrderedByJoinedAt()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        // Create 3 entries for the same course/date, different join times
        // Need to create them all in the same scope so they share the same course
        Guid courseId;
        Guid entry1Id, entry2Id, entry3Id;

        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Create tenant
            var tenant = Shadowbrook.Domain.TenantAggregate.Tenant.Create(
                $"Test Tenant {Guid.NewGuid()}",
                "Test Contact",
                "test@test.com",
                "555-0000");
            db.Tenants.Add(tenant);

            // Create course
            var course = Course.Create(tenant.Id, $"Test Course {Guid.NewGuid()}", "America/Chicago");
            db.Courses.Add(course);
            courseId = course.Id;

            // Create waitlist using reflection (private constructor)
            var waitlistType = typeof(WalkUpWaitlist);
            var waitlist = (WalkUpWaitlist)Activator.CreateInstance(waitlistType, nonPublic: true)!;
            waitlistType.GetProperty(nameof(WalkUpWaitlist.Id))!.SetValue(waitlist, Guid.CreateVersion7());
            waitlistType.GetProperty(nameof(WalkUpWaitlist.CourseId))!.SetValue(waitlist, course.Id);
            waitlistType.GetProperty(nameof(WalkUpWaitlist.Date))!.SetValue(waitlist, date);
            waitlistType.GetProperty(nameof(WalkUpWaitlist.ShortCode))!.SetValue(waitlist, $"{Random.Shared.Next(1000, 9999)}");
            waitlistType.GetProperty(nameof(WalkUpWaitlist.Status))!.SetValue(waitlist, WaitlistStatus.Open);
            waitlistType.GetProperty(nameof(WalkUpWaitlist.OpenedAt))!.SetValue(waitlist, DateTimeOffset.UtcNow);
            waitlistType.GetProperty(nameof(WalkUpWaitlist.CreatedAt))!.SetValue(waitlist, DateTimeOffset.UtcNow);
            db.CourseWaitlists.Add(waitlist);

            // Create 3 golfers and entries with different join times
            var baseTime = DateTimeOffset.UtcNow;

            var golfer1 = Golfer.Create($"+1555{Random.Shared.Next(1000000, 9999999)}", "Test", "Golfer1");
            db.Golfers.Add(golfer1);
            var entry1 = CreateEntry(waitlist.Id, golfer1.Id, new TimeOnly(10, 0), new TimeOnly(10, 30), baseTime.Add(TimeSpan.FromMinutes(10)));
            db.GolferWaitlistEntries.Add(entry1);
            entry1Id = entry1.Id;

            var golfer2 = Golfer.Create($"+1555{Random.Shared.Next(1000000, 9999999)}", "Test", "Golfer2");
            db.Golfers.Add(golfer2);
            var entry2 = CreateEntry(waitlist.Id, golfer2.Id, new TimeOnly(10, 0), new TimeOnly(10, 30), baseTime.Add(TimeSpan.FromMinutes(5)));
            db.GolferWaitlistEntries.Add(entry2);
            entry2Id = entry2.Id;

            var golfer3 = Golfer.Create($"+1555{Random.Shared.Next(1000000, 9999999)}", "Test", "Golfer3");
            db.Golfers.Add(golfer3);
            var entry3 = CreateEntry(waitlist.Id, golfer3.Id, new TimeOnly(10, 0), new TimeOnly(10, 30), baseTime.Add(TimeSpan.FromMinutes(15)));
            db.GolferWaitlistEntries.Add(entry3);
            entry3Id = entry3.Id;

            await db.SaveChangesAsync();
        }

        var entries = await FindEligibleAsync(courseId, date, new TimeOnly(10, 15));

        Assert.Equal(3, entries.Count);
        // Should be ordered by JoinedAt (entry2 earliest, entry1 middle, entry3 latest)
        Assert.Equal(entry2Id, entries[0].Id);
        Assert.Equal(entry1Id, entries[1].Id);
        Assert.Equal(entry3Id, entries[2].Id);
    }

    private static WalkUpGolferWaitlistEntry CreateEntry(
        Guid waitlistId,
        Guid golferId,
        TimeOnly windowStart,
        TimeOnly windowEnd,
        DateTimeOffset joinedAt)
    {
        var entryType = typeof(WalkUpGolferWaitlistEntry);
        var entry = (WalkUpGolferWaitlistEntry)Activator.CreateInstance(entryType, nonPublic: true)!;
        var baseType = typeof(GolferWaitlistEntry);

        baseType.GetProperty(nameof(GolferWaitlistEntry.Id))!.SetValue(entry, Guid.CreateVersion7());
        baseType.GetProperty(nameof(GolferWaitlistEntry.CourseWaitlistId))!.SetValue(entry, waitlistId);
        baseType.GetProperty(nameof(GolferWaitlistEntry.GolferId))!.SetValue(entry, golferId);
        baseType.GetProperty(nameof(GolferWaitlistEntry.IsWalkUp))!.SetValue(entry, true);
        baseType.GetProperty(nameof(GolferWaitlistEntry.GroupSize))!.SetValue(entry, 1);
        baseType.GetProperty(nameof(GolferWaitlistEntry.WindowStart))!.SetValue(entry, windowStart);
        baseType.GetProperty(nameof(GolferWaitlistEntry.WindowEnd))!.SetValue(entry, windowEnd);
        baseType.GetProperty(nameof(GolferWaitlistEntry.JoinedAt))!.SetValue(entry, joinedAt);
        baseType.GetProperty(nameof(GolferWaitlistEntry.CreatedAt))!.SetValue(entry, joinedAt);

        return entry;
    }

    // -------------------------------------------------------------------------
    // Helper Methods
    // -------------------------------------------------------------------------

    private async Task<(Guid courseId, DateOnly date, Guid entryId)> CreateEntryAsync(
        TimeOnly windowStart,
        TimeOnly windowEnd,
        int groupSize = 1)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var entryId = await CreateSingleEntryAsync(date, windowStart, windowEnd, groupSize);

        Guid courseId;
        using (var scope = this.factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var entry = await db.GolferWaitlistEntries.FindAsync(entryId);
            var waitlist = await db.CourseWaitlists.FindAsync(entry!.CourseWaitlistId);
            courseId = waitlist!.CourseId;
        }

        return (courseId, date, entryId);
    }

    private async Task<Guid> CreateSingleEntryAsync(
        DateOnly date,
        TimeOnly windowStart,
        TimeOnly windowEnd,
        int groupSize = 1,
        TimeSpan? joinedAtOffset = null)
    {
        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Create tenant
        var tenant = Shadowbrook.Domain.TenantAggregate.Tenant.Create(
            $"Test Tenant {Guid.NewGuid()}",
            "Test Contact",
            "test@test.com",
            "555-0000");
        db.Tenants.Add(tenant);

        // Create course
        var course = Course.Create(tenant.Id, $"Test Course {Guid.NewGuid()}", "America/Chicago");
        db.Courses.Add(course);

        // Create waitlist using reflection (private constructor)
        var waitlistType = typeof(WalkUpWaitlist);
        var waitlist = (WalkUpWaitlist)Activator.CreateInstance(waitlistType, nonPublic: true)!;
        waitlistType.GetProperty(nameof(WalkUpWaitlist.Id))!.SetValue(waitlist, Guid.CreateVersion7());
        waitlistType.GetProperty(nameof(WalkUpWaitlist.CourseId))!.SetValue(waitlist, course.Id);
        waitlistType.GetProperty(nameof(WalkUpWaitlist.Date))!.SetValue(waitlist, date);
        waitlistType.GetProperty(nameof(WalkUpWaitlist.ShortCode))!.SetValue(waitlist, $"{Random.Shared.Next(1000, 9999)}");
        waitlistType.GetProperty(nameof(WalkUpWaitlist.Status))!.SetValue(waitlist, WaitlistStatus.Open);
        waitlistType.GetProperty(nameof(WalkUpWaitlist.OpenedAt))!.SetValue(waitlist, DateTimeOffset.UtcNow);
        waitlistType.GetProperty(nameof(WalkUpWaitlist.CreatedAt))!.SetValue(waitlist, DateTimeOffset.UtcNow);
        db.CourseWaitlists.Add(waitlist);

        // Create golfer
        var golfer = Golfer.Create($"+1555{Random.Shared.Next(1000000, 9999999)}", "Test", "Golfer");
        db.Golfers.Add(golfer);

        // Create entry using reflection (constructor is internal)
        var entryType = typeof(WalkUpGolferWaitlistEntry);
        var entry = (WalkUpGolferWaitlistEntry)Activator.CreateInstance(entryType, nonPublic: true)!;
        var baseType = typeof(GolferWaitlistEntry);

        var joinedAt = DateTimeOffset.UtcNow.Add(joinedAtOffset ?? TimeSpan.Zero);

        baseType.GetProperty(nameof(GolferWaitlistEntry.Id))!.SetValue(entry, Guid.CreateVersion7());
        baseType.GetProperty(nameof(GolferWaitlistEntry.CourseWaitlistId))!.SetValue(entry, waitlist.Id);
        baseType.GetProperty(nameof(GolferWaitlistEntry.GolferId))!.SetValue(entry, golfer.Id);
        baseType.GetProperty(nameof(GolferWaitlistEntry.IsWalkUp))!.SetValue(entry, true);
        baseType.GetProperty(nameof(GolferWaitlistEntry.GroupSize))!.SetValue(entry, groupSize);
        baseType.GetProperty(nameof(GolferWaitlistEntry.WindowStart))!.SetValue(entry, windowStart);
        baseType.GetProperty(nameof(GolferWaitlistEntry.WindowEnd))!.SetValue(entry, windowEnd);
        baseType.GetProperty(nameof(GolferWaitlistEntry.JoinedAt))!.SetValue(entry, joinedAt);
        baseType.GetProperty(nameof(GolferWaitlistEntry.CreatedAt))!.SetValue(entry, joinedAt);

        db.GolferWaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        return entry.Id;
    }

    private async Task<List<GolferWaitlistEntry>> FindEligibleAsync(
        Guid courseId,
        DateOnly date,
        TimeOnly teeTime,
        int maxGroupSize = 4)
    {
        using var scope = this.factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = new GolferWaitlistEntryRepository(db);

        return await repo.FindEligibleEntriesAsync(courseId, date, teeTime, maxGroupSize);
    }
}

internal class TestTimeProvider : ITimeProvider
{
    public DateOnly GetCurrentDate() => DateOnly.FromDateTime(DateTime.UtcNow);
    public TimeOnly GetCurrentTime() => TimeOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly GetCurrentDateByTimeZone(string timeZoneId) => DateOnly.FromDateTime(DateTime.UtcNow);
    public TimeOnly GetCurrentTimeByTimeZone(string timeZoneId) => TimeOnly.FromDateTime(DateTime.UtcNow);
    public DateTimeOffset GetCurrentTimestamp() => DateTimeOffset.UtcNow;
}
