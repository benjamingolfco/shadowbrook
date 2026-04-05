using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeOpeningAggregate.Exceptions;

public class InvalidGroupSizeException()
    : DomainException("Group size must be at least 1.");
