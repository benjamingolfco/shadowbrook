using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeSheetAggregate;

public class TeeSheetInterval : Entity
{
    public Guid TeeSheetId { get; private set; }
    public TimeOnly Time { get; private set; }
    public int Capacity { get; private set; }

    private TeeSheetInterval() { } // EF

    internal TeeSheetInterval(Guid teeSheetId, TimeOnly time, int capacity)
    {
        Id = Guid.CreateVersion7();
        TeeSheetId = teeSheetId;
        Time = time;
        Capacity = capacity;
    }
}
