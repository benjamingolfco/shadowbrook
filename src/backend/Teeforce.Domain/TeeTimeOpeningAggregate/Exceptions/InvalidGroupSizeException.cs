using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.TeeTimeOpeningAggregate.Exceptions;

public class InvalidGroupSizeException()
    : DomainException("Group size must be at least 1.");
