using System.ComponentModel.DataAnnotations;

namespace ORBIT.API.Models;

public class RequestPasswordResetRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class VerifyPasswordResetCodeRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^[0-9]{6}$")]
    public string Code { get; set; } = string.Empty;
}

public class CompletePasswordResetRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string ResetToken { get; set; } = string.Empty;

    [Required]
    public string NewPassword { get; set; } = string.Empty;
}
