using System.Security.Cryptography;
using System.Text;

namespace ORBIT.API.Services;

public class PasswordResetSecurity(IConfiguration configuration)
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(
        configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is not configured."));

    public string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public string Hash(string value)
    {
        using var hmac = new HMACSHA256(_key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    public bool Verify(string value, string expectedHash) =>
        CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(Hash(value)),
            Convert.FromHexString(expectedHash));
}
