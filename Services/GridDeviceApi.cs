using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ORBIT.API.Data;
using ORBIT.API.Models;

namespace ORBIT.API.Services;

public static class GridDeviceApi
{
    public const int HeartbeatIntervalSeconds = 300;

    public static void MapGridDeviceApi(this WebApplication app)
    {
        app.MapPost("/api/devices/{deviceId:int}/device-credential", IssueCredentialAsync)
            .RequireAuthorization()
            .WithName("IssueGridDeviceCredential")
            .WithOpenApi();

        app.MapPost("/api/device/v1/heartbeat", HeartbeatAsync)
            .AllowAnonymous()
            .WithName("GridDeviceHeartbeat")
            .WithOpenApi();
    }

    private static async Task<IResult> IssueCredentialAsync(
        int deviceId,
        ClaimsPrincipal principal,
        AppDbContext db,
        DeviceApiSecurity security,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(principal.FindFirstValue("userId"), out var userId))
        {
            return Results.Unauthorized();
        }

        var device = await db.Devices
            .FirstOrDefaultAsync(item =>
                item.Id == deviceId &&
                item.Aquarium != null &&
                item.Aquarium.UserId == userId,
                cancellationToken);

        if (device is null)
        {
            return Results.NotFound(new { message = "Cihaz bulunamadı." });
        }

        if (!string.Equals(device.DeviceType, "GRID", StringComparison.Ordinal) ||
            device.ManufacturerId != DeviceIdentity.TwinsManufacturerId ||
            device.ImmutableDeviceId is null ||
            device.ProductTypeCode != 1)
        {
            return Results.BadRequest(new { message = "Cihaz, GRID Device Identity v1 kaydı değil." });
        }

        var secret = security.CreateSecret();
        device.DeviceCredentialHash = security.HashSecret(secret);
        device.DeviceCredentialCreatedAt = DateTime.UtcNow;
        device.LastSeenAt = null;
        device.IsOnline = false;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            credentialVersion = 1,
            authenticationScheme = DeviceApiSecurity.AuthenticationScheme,
            manufacturerId = device.ManufacturerId,
            deviceId = device.ImmutableDeviceId,
            deviceSecret = secret,
            heartbeatPath = "/api/device/v1/heartbeat",
            heartbeatIntervalSeconds = HeartbeatIntervalSeconds
        });
    }

    private static async Task<IResult> HeartbeatAsync(
        GridTelemetryRequest telemetry,
        HttpContext httpContext,
        AppDbContext db,
        DeviceApiSecurity security,
        IHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.IsHttps && !environment.IsDevelopment())
        {
            return Results.Json(
                new { message = "HTTPS gerekli." },
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!security.TryReadRequestCredential(
                httpContext.Request,
                out var manufacturerId,
                out var immutableDeviceId,
                out var secret) ||
            manufacturerId != DeviceIdentity.TwinsManufacturerId)
        {
            return Results.Unauthorized();
        }

        var device = await db.Devices.FirstOrDefaultAsync(item =>
            item.ManufacturerId == manufacturerId &&
            item.ImmutableDeviceId == immutableDeviceId &&
            item.ProductTypeCode == 1 &&
            item.DeviceType == "GRID",
            cancellationToken);

        if (device is null || !security.VerifySecret(secret, device.DeviceCredentialHash))
        {
            return Results.Unauthorized();
        }

        var now = DateTime.UtcNow;
        device.LastSeenAt = now;
        device.IsOnline = true;
        db.DeviceTelemetry.Add(new DeviceTelemetry
        {
            DeviceId = device.Id,
            ReceivedAt = now,
            UptimeSeconds = telemetry.UptimeSeconds,
            InputVoltage = Math.Clamp(telemetry.InputVoltage, 0, 400),
            TemperatureC = telemetry.TemperatureC is >= -55 and <= 125
                ? telemetry.TemperatureC
                : null,
            WaterAlarm = telemetry.WaterAlarm,
            RelayMask = telemetry.RelayMask
        });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            serverTimeUtc = now,
            nextHeartbeatSeconds = HeartbeatIntervalSeconds,
            telemetry = new
            {
                enabled = false,
                intervalSeconds = HeartbeatIntervalSeconds
            }
        });
    }
}

public sealed record GridTelemetryRequest(
    uint UptimeSeconds,
    float InputVoltage,
    float? TemperatureC,
    bool WaterAlarm,
    byte RelayMask);
