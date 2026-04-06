using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.Common;

namespace Teeforce.Api.Features.Waitlist.Handlers;

public record WaitlistJoined(string CourseName) : INotification;

public class WaitlistJoinedSmsFormatter : ISmsFormatter<WaitlistJoined>
{
    public string Format(WaitlistJoined n) =>
        $"You're on the waitlist at {n.CourseName}. Keep your phone handy - we'll text you when a spot opens up!";
}
