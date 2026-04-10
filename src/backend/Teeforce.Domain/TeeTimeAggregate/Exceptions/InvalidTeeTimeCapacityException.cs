using Teeforce.Domain.Common;

namespace Teeforce.Domain.TeeTimeAggregate.Exceptions;

public class InvalidTeeTimeCapacityException() : DomainException("Tee time capacity must be positive.");
