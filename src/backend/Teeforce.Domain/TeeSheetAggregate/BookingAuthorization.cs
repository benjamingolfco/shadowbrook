namespace Teeforce.Domain.TeeSheetAggregate;

public sealed class BookingAuthorization
{
    public Guid SheetId { get; }

    internal BookingAuthorization(Guid sheetId)
    {
        SheetId = sheetId;
    }
}
