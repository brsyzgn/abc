using ORBIT.API.Data;
using ORBIT.API.Models;

namespace ORBIT.API.Services;

public static class ActivityRecorder
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public static bool IsExpired(DateTime createdAt, DateTime now) =>
        createdAt < now.Subtract(Retention);

    public static void Add(
        AppDbContext dbContext,
        int userId,
        string eventType,
        string title,
        string message) =>
        dbContext.ActivityEvents.Add(new ActivityEvent
        {
            UserId = userId,
            EventType = eventType,
            Title = title,
            Message = message,
            CreatedAt = DateTime.UtcNow
        });
}
