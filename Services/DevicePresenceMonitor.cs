using Microsoft.EntityFrameworkCore;
using ORBIT.API.Data;

namespace ORBIT.API.Services;

public sealed class DevicePresenceMonitor(IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await MarkStaleGridDevicesOfflineAsync(stoppingToken);
        }
    }

    private async Task MarkStaleGridDevicesOfflineAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - OfflineAfter;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Devices
            .Where(device =>
                device.DeviceType == "GRID" &&
                device.ProductTypeCode == 1 &&
                device.DeviceCredentialHash != null &&
                device.IsOnline &&
                (device.LastSeenAt == null || device.LastSeenAt < cutoff))
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(device => device.IsOnline, false),
                cancellationToken);
    }
}
