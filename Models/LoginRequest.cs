using System.ComponentModel.DataAnnotations;

namespace ORBIT.API.Models;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string DeviceId { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public bool Takeover { get; set; }
}
