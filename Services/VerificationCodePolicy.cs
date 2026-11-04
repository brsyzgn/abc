namespace ORBIT.API.Services;

public static class VerificationCodePolicy
{
    public const int MaximumFailedAttempts = 5;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);
    public static bool IsExpired(DateTime expiresAt, DateTime now) => expiresAt <= now;
}
