using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Domain.Common;

public static class RepositoryExtensions
{
    public static async Task<Booking> GetRequiredByIdAsync(this IBookingRepository repo, Guid id) =>
        await repo.GetByIdAsync(id) ?? throw new EntityNotFoundException(nameof(Booking), id);

    public static async Task<Course> GetRequiredByIdAsync(this ICourseRepository repo, Guid id) =>
        await repo.GetByIdAsync(id) ?? throw new EntityNotFoundException(nameof(Course), id);

    public static async Task<CourseWaitlist> GetRequiredByIdAsync(this ICourseWaitlistRepository repo, Guid id) =>
        await repo.GetByIdAsync(id) ?? throw new EntityNotFoundException(nameof(CourseWaitlist), id);

    public static async Task<Golfer> GetRequiredByIdAsync(this IGolferRepository repo, Guid id) =>
        await repo.GetByIdAsync(id) ?? throw new EntityNotFoundException(nameof(Golfer), id);

    public static async Task<GolferWaitlistEntry> GetRequiredByIdAsync(this IGolferWaitlistEntryRepository repo, Guid id) =>
        await repo.GetByIdAsync(id) ?? throw new EntityNotFoundException(nameof(GolferWaitlistEntry), id);

    public static async Task<TeeTimeOpening> GetRequiredByIdAsync(this ITeeTimeOpeningRepository repo, Guid id) =>
        await repo.GetByIdAsync(id) ?? throw new EntityNotFoundException(nameof(TeeTimeOpening), id);

    public static async Task<WaitlistOffer> GetRequiredByIdAsync(this IWaitlistOfferRepository repo, Guid id) =>
        await repo.GetByIdAsync(id) ?? throw new EntityNotFoundException(nameof(WaitlistOffer), id);
}
