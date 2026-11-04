using System.ComponentModel.DataAnnotations;

namespace ORBIT.API.Models;

public class UpdateDeviceRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
}
