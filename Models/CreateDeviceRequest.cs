using System.ComponentModel.DataAnnotations;

namespace ORBIT.API.Models;

public class CreateDeviceRequest : IValidatableObject
{
    public string? DeviceType { get; set; } = string.Empty;
    public string? Name { get; set; } = string.Empty;
    public string? SerialNumber { get; set; } = string.Empty;
    public DeviceIdentity? Identity { get; set; }
    public DeviceConnectionDescriptor? ConnectionDescriptor { get; set; }
    public string? UsedDeviceAction { get; set; }

    // One validation path for the endpoint and DataAnnotations consumers.
    // Display name and optional legacy serial never construct the v1 key.
    public string? GetValidationError(out string deviceType, out string name, out string serialNumber)
    {
        deviceType = Identity?.ProductName ?? (DeviceType ?? "").Trim().ToUpperInvariant();
        name = (Name ?? "").Trim();
        serialNumber = (SerialNumber ?? "").Trim().ToUpperInvariant();
        if (ConnectionDescriptor is not null && !ConnectionDescriptor.Matches(Identity))
            return "Bağlantı tanımı cihaz kimliğiyle eşleşmiyor veya geçersiz.";
        if (Identity is not null)
        {
            if (!Identity.IsValid) return "Geçersiz Device Identity v1.";
            if (name.Length == 0) name = Identity.ProductName;
        }
        if (deviceType is not ("GRID" or "LYRA" or "DRIP"))
            return "Cihaz tipi GRID, LYRA veya DRIP olmalıdır.";
        if (Identity is null && (name.Length == 0 || serialNumber.Length == 0))
            return "Cihaz adı ve seri numarası zorunludur.";
        if (name.Length > 100 || serialNumber.Length > 100)
            return "Cihaz adı ve seri numarası en fazla 100 karakter olabilir.";
        return null;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var error = GetValidationError(out _, out _, out _);
        if (error is not null) yield return new ValidationResult(error);
    }
}
