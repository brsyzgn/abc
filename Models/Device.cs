using System;
using System.ComponentModel.DataAnnotations;

namespace ORBIT.API.Models
{
    public class Device : IDeviceIdentityFields
    {
        public int Id { get; set; }
        [StringLength(32)]
        public string? ConnectionSsid { get; set; }
        [StringLength(256)]
        public string? LocalApiBaseUrl { get; set; }
    public Guid? ManufacturerId { get; set; }
    public Guid? ImmutableDeviceId { get; set; }
    public int? IdentitySchemaVersion { get; set; }
    public int? ProductTypeCode { get; set; }
    public int? HardwareRevision { get; set; }
    public int? FirmwareMajor { get; set; }
    public int? FirmwareMinor { get; set; }
    public int? FirmwarePatch { get; set; }

        [StringLength(64)]
        public string? DeviceCredentialHash { get; set; }

        public DateTime? DeviceCredentialCreatedAt { get; set; }

        public DateTime? LastSeenAt { get; set; }


        [Required]
        public int AquariumId { get; set; }

        [Required]
        [StringLength(20)]
        [RegularExpression("^(GRID|LYRA|DRIP)$", ErrorMessage = "DeviceType must be GRID, LYRA or DRIP.")]
        public string DeviceType { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string SerialNumber { get; set; } = string.Empty;

        public bool IsOnline { get; set; } = false;

        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Aquarium? Aquarium { get; set; }
    }
}
