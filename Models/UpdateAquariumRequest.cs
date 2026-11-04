using System.ComponentModel.DataAnnotations;

namespace ORBIT.API.Models;

public class UpdateAquariumRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? PhotoUrl { get; set; }
}
