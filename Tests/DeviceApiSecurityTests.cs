using ORBIT.API.Services;

static class DeviceApiSecurityTests
{
    public static void Run()
    {
        var security = new DeviceApiSecurity();
        var first = security.CreateSecret();
        var second = security.CreateSecret();
        Assert(first != second, "Device credentials must be independently random.");
        Assert(first.Length >= 32, "Device credential entropy is unexpectedly low.");

        var hash = security.HashSecret(first);
        Assert(hash.Length == 64, "SHA-256 device credential hash must be 32 bytes.");
        Assert(security.VerifySecret(first, hash), "Valid device credential was rejected.");
        Assert(!security.VerifySecret(second, hash), "Wrong device credential was accepted.");
        Assert(!security.VerifySecret(Guid.NewGuid().ToString("D"), hash),
            "A device ID must not authenticate as a device credential.");
        Assert(!security.VerifySecret(first, null), "Missing credential hash must fail closed.");
        Assert(!security.VerifySecret(first, "not-hex"), "Malformed credential hash must fail closed.");

        Console.WriteLine("Device API security verification passed.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
