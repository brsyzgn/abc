using System.ComponentModel.DataAnnotations;

namespace ORBIT.API.Models;

public class UserSession
{
    public Guid Id { get; set; }
    public int UserId { get; set; }

    [Required]
    [StringLength(200)]
    public string DeviceId { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public User? User { get; set; }
}
