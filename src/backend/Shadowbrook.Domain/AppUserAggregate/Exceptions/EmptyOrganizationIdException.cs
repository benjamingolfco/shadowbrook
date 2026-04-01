using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.AppUserAggregate.Exceptions;

public class EmptyOrganizationIdException()
    : DomainException("OrganizationId must not be empty.");
