using Teeforce.Domain.Common;

namespace Teeforce.Domain.AppUserAggregate.Exceptions;

public class DuplicateEmailException(string email)
    : DomainException($"A user with email '{email}' already exists.");
