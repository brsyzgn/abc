using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ORBIT.API.Models
{
    public class Aquarium
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? PhotoUrl { get; set; }

        [StringLength(64)]
        public string? PhotoObjectKey { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }

        public ICollection<Device>? Devices { get; set; }
    }
}
