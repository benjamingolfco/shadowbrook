using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.AppUserAggregate.Exceptions;

public class IdentityAlreadyLinkedException()
    : DomainException("This user is already linked to a different identity.");
