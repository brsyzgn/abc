using System.ComponentModel.DataAnnotations;

namespace ORBIT.API.Models;

public sealed class DeviceTelemetry
{
    public long Id { get; set; }
    [Required]
    public int DeviceId { get; set; }
    public DateTime ReceivedAt { get; set; }
    public uint UptimeSeconds { get; set; }
    public float InputVoltage { get; set; }
    public float? TemperatureC { get; set; }
    public bool WaterAlarm { get; set; }
    public byte RelayMask { get; set; }
}
