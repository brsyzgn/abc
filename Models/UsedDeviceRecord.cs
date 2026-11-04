using System.ComponentModel.DataAnnotations;

namespace ORBIT.API.Models;

public class UsedDeviceRecord : IDeviceIdentityFields
{
    public int Id { get; set; }
    public Guid? ManufacturerId { get; set; }
    public Guid? ImmutableDeviceId { get; set; }
    public int? IdentitySchemaVersion { get; set; }
    public int? ProductTypeCode { get; set; }
    public int? HardwareRevision { get; set; }
    public int? FirmwareMajor { get; set; }
    public int? FirmwareMinor { get; set; }
    public int? FirmwarePatch { get; set; }

    [Required, StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;
    [Required, StringLength(20)]
    public string DeviceType { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;
    public int LastUserId { get; set; }
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
}
