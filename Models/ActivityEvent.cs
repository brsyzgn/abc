using System.ComponentModel.DataAnnotations;

namespace ORBIT.API.Models;

public class ActivityEvent
{
    public long Id { get; set; }
    public int UserId { get; set; }
    [Required, StringLength(50)] public string EventType { get; set; } = string.Empty;
    [Required, StringLength(120)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(500)] public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User? User { get; set; }
}
