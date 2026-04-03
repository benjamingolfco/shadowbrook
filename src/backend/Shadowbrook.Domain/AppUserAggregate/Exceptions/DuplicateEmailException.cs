using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.AppUserAggregate.Exceptions;

public class DuplicateEmailException(string email)
    : DomainException($"A user with email '{email}' already exists.");
