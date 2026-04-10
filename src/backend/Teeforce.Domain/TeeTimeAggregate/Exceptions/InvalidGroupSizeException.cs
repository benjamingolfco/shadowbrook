using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Exceptions;

public class InvalidGroupSizeException() : DomainException("Group size must be positive.");
