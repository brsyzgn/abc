using System.ComponentModel.DataAnnotations;

namespace ORBIT.API.Models;

public class RegistrationVerificationChallenge
{
    public long Id { get; set; }
    public int UserId { get; set; }
    [Required, StringLength(128)]
    public string CodeHash { get; set; } = string.Empty;
    public int FailedAttempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public User? User { get; set; }
}
