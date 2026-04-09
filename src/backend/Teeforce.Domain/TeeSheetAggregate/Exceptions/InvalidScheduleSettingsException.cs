using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeSheetAggregate.Exceptions;

public class InvalidScheduleSettingsException(string message) : DomainException(message)
{
}
