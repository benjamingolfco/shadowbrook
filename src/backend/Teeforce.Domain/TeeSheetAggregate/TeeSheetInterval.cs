using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeSheetAggregate;

public class TeeSheetInterval : Entity
{
    public Guid TeeSheetId { get; private set; }
    public TimeOnly Time { get; private set; }
    public int Capacity { get; private set; }
    public decimal? Price { get; private set; }
    public Guid? RateScheduleId { get; private set; }

    private TeeSheetInterval() { } // EF

    internal TeeSheetInterval(Guid teeSheetId, TimeOnly time, int capacity)
    {
        Id = Guid.CreateVersion7();
        TeeSheetId = teeSheetId;
        Time = time;
        Capacity = capacity;
    }

    internal void SetPricing(decimal? price, Guid? rateScheduleId)
    {
        Price = price;
        RateScheduleId = rateScheduleId;
    }
}
