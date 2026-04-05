using Teeforce.Domain.Common;

namespace Teeforce.Domain.AppUserAggregate.Exceptions;

public class EmptyOrganizationIdException()
    : DomainException("OrganizationId must not be empty.");
