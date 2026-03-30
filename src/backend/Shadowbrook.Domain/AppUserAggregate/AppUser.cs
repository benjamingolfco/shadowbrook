using Shadowbrook.Domain.Common;

namespace Shadowbrook.Domain.AppUserAggregate;

public class AppUser : Entity
{
    public string IdentityId { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public AppUserRole Role { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    private readonly List<CourseAssignment> courseAssignments = [];
    public IReadOnlyCollection<CourseAssignment> CourseAssignments => this.courseAssignments.AsReadOnly();

    private AppUser() { } // EF

    public static AppUser Create(
        string identityId, string email, string displayName,
        AppUserRole role, Guid? organizationId)
    {
        return new AppUser
        {
            Id = Guid.CreateVersion7(),
            IdentityId = identityId,
            Email = email.Trim(),
            DisplayName = displayName.Trim(),
            Role = role,
            OrganizationId = organizationId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void RecordLogin() => LastLoginAt = DateTimeOffset.UtcNow;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public CourseAssignment AssignCourse(Guid courseId)
    {
        if (this.courseAssignments.Any(a => a.CourseId == courseId))
        {
            throw new InvalidOperationException($"User is already assigned to course {courseId}.");
        }

        var assignment = CourseAssignment.Create(Id, courseId);
        this.courseAssignments.Add(assignment);
        return assignment;
    }

    public void UnassignCourse(Guid courseId)
    {
        var assignment = this.courseAssignments.FirstOrDefault(a => a.CourseId == courseId)
            ?? throw new InvalidOperationException($"User is not assigned to course {courseId}.");
        this.courseAssignments.Remove(assignment);
    }
}
