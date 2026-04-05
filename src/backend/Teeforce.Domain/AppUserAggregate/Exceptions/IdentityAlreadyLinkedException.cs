using Teeforce.Domain.Common;

namespace Teeforce.Domain.AppUserAggregate.Exceptions;

public class IdentityAlreadyLinkedException()
    : DomainException("This user is already linked to a different identity.");
