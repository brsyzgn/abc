using System.ComponentModel.DataAnnotations;

namespace ORBIT.API.Models;

public class PasswordResetChallenge
{
    public long Id { get; set; }
    public int UserId { get; set; }

    [Required]
    [StringLength(128)]
    public string CodeHash { get; set; } = string.Empty;

    [StringLength(128)]
    public string? ResetTokenHash { get; set; }

    public int FailedAttempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? CodeVerifiedAt { get; set; }
    public DateTime? ResetTokenExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public User? User { get; set; }
}
