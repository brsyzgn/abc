using System.Text.Json.Serialization;
namespace ORBIT.API.Models;

public interface IDeviceIdentityFields
{
    Guid? ManufacturerId { get; set; }
    Guid? ImmutableDeviceId { get; set; }
    int? IdentitySchemaVersion { get; set; }
    int? ProductTypeCode { get; set; }
    int? HardwareRevision { get; set; }
    int? FirmwareMajor { get; set; }
    int? FirmwareMinor { get; set; }
    int? FirmwarePatch { get; set; }

}

public sealed record FirmwareVersion(int Major, int Minor, int Patch);

public sealed record DeviceIdentity(
    Guid ManufacturerId, Guid DeviceId, int IdentitySchemaVersion,
    int ProductType, int HardwareRevision, FirmwareVersion? FirmwareVersion)
{
    public static readonly Guid TwinsManufacturerId = Guid.Parse("40b635de-f7d1-48e6-ae89-0977a3495e3e");
    [JsonIgnore]
    public string ProductName => ProductType switch { 1 => "GRID", 2 => "DRIP", 3 => "LYRA", _ => "" };
    [JsonIgnore]
    public string Key => $"{ManufacturerId:D}/{DeviceId:D}";
    [JsonIgnore]
    public bool IsValid => ManufacturerId == TwinsManufacturerId && DeviceId != Guid.Empty &&
        IdentitySchemaVersion == 1 && ProductType is >= 1 and <= 3 &&
        HardwareRevision is >= 1 and <= 65535 && FirmwareVersion is not null &&
        FirmwareVersion.Major is >= 0 and <= 65535 &&
        FirmwareVersion.Minor is >= 0 and <= 65535 &&
        FirmwareVersion.Patch is >= 0 and <= 65535;

    public bool CompatibleHardware(IDeviceIdentityFields row) =>
        row.ManufacturerId == ManufacturerId && row.ImmutableDeviceId == DeviceId &&
        row.ProductTypeCode == ProductType && row.HardwareRevision == HardwareRevision;

    public void ApplyTo(IDeviceIdentityFields row)
    {
        if (!IsValid) throw new ArgumentException("Invalid Device Identity v1");
        row.ManufacturerId = ManufacturerId;
        row.ImmutableDeviceId = DeviceId;
        row.IdentitySchemaVersion = IdentitySchemaVersion;
        row.ProductTypeCode = ProductType;
        row.HardwareRevision = HardwareRevision;
        row.FirmwareMajor = FirmwareVersion!.Major;
        row.FirmwareMinor = FirmwareVersion.Minor;
        row.FirmwarePatch = FirmwareVersion.Patch;
    }

    public static DeviceIdentity? From(IDeviceIdentityFields row) =>
        row.ManufacturerId is null ? null : new DeviceIdentity(
            row.ManufacturerId.Value, row.ImmutableDeviceId!.Value,
            row.IdentitySchemaVersion!.Value, row.ProductTypeCode!.Value,
            row.HardwareRevision!.Value,
            new FirmwareVersion(row.FirmwareMajor!.Value, row.FirmwareMinor!.Value, row.FirmwarePatch!.Value));
}
