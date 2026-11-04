using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ORBIT.API.Models;

// Non-secret routing data only. Never store credentials or infer identity here.
public sealed record DeviceConnectionDescriptor(
    int DescriptorVersion, Guid ManufacturerId, Guid DeviceId,
    string WifiMode, string Ssid, string LocalApiBaseUrl)
{
    public bool Matches(DeviceIdentity? identity)
    {
        if (identity is null || DescriptorVersion != 1 || WifiMode != "ap" ||
            ManufacturerId != identity.ManufacturerId || DeviceId != identity.DeviceId ||
            string.IsNullOrEmpty(Ssid) || Encoding.UTF8.GetByteCount(Ssid) > 32 || Ssid.Contains('\0') ||
            string.IsNullOrEmpty(LocalApiBaseUrl) || LocalApiBaseUrl.Length > 256 ||
            !Uri.TryCreate(LocalApiBaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/") return false;
        if (uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) && uri.Host.Length > 6) return true;
        if (!IPAddress.TryParse(uri.Host, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) ||
            (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254);
    }

    public void ApplyTo(Device device)
    {
        if (!Matches(DeviceIdentity.From(device))) throw new ArgumentException("Invalid connection descriptor");
        device.ConnectionSsid = Ssid;
        device.LocalApiBaseUrl = LocalApiBaseUrl;
    }

    public static DeviceConnectionDescriptor? From(Device device) =>
        device.ManufacturerId is null || device.ConnectionSsid is null || device.LocalApiBaseUrl is null
            ? null : new(1, device.ManufacturerId.Value, device.ImmutableDeviceId!.Value,
                "ap", device.ConnectionSsid, device.LocalApiBaseUrl);
}
