using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace ORBIT.API.Services;

public sealed class DeviceApiSecurity
{
    public const string AuthenticationScheme = "TwinS-Device";
    private const int SecretByteLength = 32;
    private const int MaximumSecretLength = 128;

    public string CreateSecret() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretByteLength));

    public string HashSecret(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    public bool VerifySecret(string secret, string? expectedHash)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length > MaximumSecretLength ||
            string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return expected.Length == actual.Length &&
            CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public bool TryReadRequestCredential(
        HttpRequest request,
        out Guid manufacturerId,
        out Guid deviceId,
        out string secret)
    {
        manufacturerId = Guid.Empty;
        deviceId = Guid.Empty;
        secret = string.Empty;

        if (!Guid.TryParse(request.Headers["X-TwinS-Manufacturer-Id"], out manufacturerId) ||
            !Guid.TryParse(request.Headers["X-TwinS-Device-Id"], out deviceId) ||
            manufacturerId == Guid.Empty || deviceId == Guid.Empty)
        {
            return false;
        }

        var authorization = request.Headers.Authorization.ToString();
        var prefix = AuthenticationScheme + " ";
        if (!authorization.StartsWith(prefix, StringComparison.Ordinal) ||
            authorization.Length <= prefix.Length)
        {
            return false;
        }

        secret = authorization[prefix.Length..].Trim();
        if (secret.Length is < 32 or > MaximumSecretLength || secret.Any(char.IsWhiteSpace))
        {
            secret = string.Empty;
            return false;
        }

        return true;
    }
}
