using System.ComponentModel.DataAnnotations;

namespace ORBIT.API.Models
{
    public class CreateAquariumRequest
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? PhotoUrl { get; set; }
    }
}
