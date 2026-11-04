using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ORBIT.API.Data;
using ORBIT.API.Models;

namespace ORBIT.API.Services;

/// <summary>
/// Migration-free transaction journal in ActivityEvents. Internal pairing.* rows
/// are not user activity and must not be pruned: aborted IDs are tombstones.
/// Serializable transactions also serialize an abort arriving before a slow PUT.
/// </summary>
public static class PairingTransactions
{
    public const string PendingNew = "pairing.pending.new";
    const string PendingExisting = "pairing.pending.existing";
    const string CompleteNew = "pairing.complete.new";
    const string CompleteExisting = "pairing.complete.existing";
    const string Aborted = "pairing.aborted";
    public static bool OwnsNewRecord(string state) => state is PendingNew or CompleteNew;

    public static void MapPairingTransactions(this WebApplication app)
    {
        app.MapGet("/api/pairing-transactions/{attemptId:guid}",
            Status).RequireAuthorization();
        app.MapPut("/api/pairing-transactions/{attemptId:guid}/aquariums/{aquariumId:int}",
            Begin).RequireAuthorization();
        app.MapPost("/api/pairing-transactions/{attemptId:guid}/complete",
            Complete).RequireAuthorization();
        app.MapDelete("/api/pairing-transactions/{attemptId:guid}",
            Abort).RequireAuthorization();
    }

    static int UserId(ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirstValue("userId")!);

    static object View(Device d) => new {
        id = d.Id, aquariumId = d.AquariumId, deviceType = d.DeviceType,
        name = d.Name, serialNumber = d.SerialNumber, isOnline = d.IsOnline,
        sortOrder = d.SortOrder, identity = DeviceIdentity.From(d),
        connectionDescriptor = DeviceConnectionDescriptor.From(d)
    };

    static async Task<IResult> Status(Guid attemptId, ClaimsPrincipal principal, AppDbContext db)
    {
        if (attemptId == Guid.Empty) return Results.BadRequest();
        var userId = UserId(principal);
        var key = attemptId.ToString();
        var journal = await db.ActivityEvents.AsNoTracking().SingleOrDefaultAsync(e =>
            e.UserId == userId && e.Title == key && e.EventType.StartsWith("pairing."));
        if (journal is null) return Results.Ok(new { status = "missing" });
        if (journal.EventType == Aborted) return Results.Ok(new { status = "aborted" });
        var device = await db.Devices.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(d =>
            d.Id == int.Parse(journal.Message) && d.Aquarium!.UserId == userId);
        if (device is null) return Results.Conflict(new { message = "İşlem cihaz kaydıyla uzlaştırılamadı." });
        var status = journal.EventType switch {
            PendingNew or PendingExisting => "pending",
            CompleteNew or CompleteExisting => "completed",
            _ => null
        };
        return status is null ? Results.Conflict() : Results.Ok(new { status, device = View(device) });
    }

    static async Task<IResult> Begin(Guid attemptId, int aquariumId,
        CreateDeviceRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = UserId(principal);
        var identity = request.Identity;
        var error = request.GetValidationError(out var type, out var name, out var serial);
        if (attemptId == Guid.Empty || error is not null || identity is null ||
            request.ConnectionDescriptor is null)
            return Results.BadRequest(new { message = error ?? "Device Identity v1 ve bağlantı tanımı zorunludur." });
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (!await db.Aquariums.AnyAsync(a => a.Id == aquariumId && a.UserId == userId))
            return Results.NotFound();
        var key = attemptId.ToString();
        var journal = await db.ActivityEvents.SingleOrDefaultAsync(e =>
            e.UserId == userId && e.Title == key && e.EventType.StartsWith("pairing."));
        if (journal is not null)
        {
            if (journal.EventType == Aborted)
                return Results.Conflict(new { message = "Eşleştirme işlemi iptal edilmiş." });
            var previous = await db.Devices.IgnoreQueryFilters().SingleOrDefaultAsync(d =>
                d.Id == int.Parse(journal.Message) && d.Aquarium!.UserId == userId);
            if (previous is null || previous.AquariumId != aquariumId ||
                previous.ManufacturerId != identity.ManufacturerId ||
                previous.ImmutableDeviceId != identity.DeviceId)
                return Results.Conflict(new { message = "İşlem başka bir kayda ait." });
            return Results.Ok(View(previous));
        }
        var device = await db.Devices.IgnoreQueryFilters().SingleOrDefaultAsync(d =>
            d.ManufacturerId == identity.ManufacturerId && d.ImmutableDeviceId == identity.DeviceId);
        var created = device is null;
        if (device is not null)
        {
            if (device.AquariumId != aquariumId || !identity.CompatibleHardware(device))
                return Results.Conflict(new { message = "Cihaz zaten eşleştirilmiş veya kimlik metadata'sı farklı." });
            var deviceIdText = device.Id.ToString();
            if (await db.ActivityEvents.AnyAsync(e =>
                e.Message == deviceIdText && e.EventType == PendingNew))
                return Results.Conflict(new { message = "Bu cihazın önceki eşleştirmesi henüz tamamlanmadı. Önce o işlemi yeniden deneyin veya iptal edin." });
            // Never mutate or claim an existing record as rollback-owned.
        }
        else
        {
            var used = await db.UsedDeviceRecords.SingleOrDefaultAsync(d =>
                d.ManufacturerId == identity.ManufacturerId && d.ImmutableDeviceId == identity.DeviceId);
            if (used is not null && !identity.CompatibleHardware(used))
                return Results.Conflict(new { message = "Kimlik donanım geçmişiyle uyuşmuyor." });
            var action = request.UsedDeviceAction?.Trim().ToLowerInvariant();
            if (used is not null && action is not ("keep" or "reset"))
                return Results.Conflict(new {
                    code = "used_device_choice_required",
                    message = "Bu cihaz daha önce kullanılmış. ORBIT ayarlarını sıfırlamayı veya korumayı seçin."
                });
            device = new Device {
                AquariumId = aquariumId, DeviceType = type,
                Name = used is not null && action == "keep" && used.LastUserId == userId ? used.LastName : name,
                SerialNumber = serial, IsOnline = true,
                SortOrder = (await db.Devices.Where(d => d.AquariumId == aquariumId)
                    .MaxAsync(d => (int?)d.SortOrder) ?? -1) + 1
            };
            identity.ApplyTo(device);
            request.ConnectionDescriptor.ApplyTo(device);
            db.Devices.Add(device);
            await db.SaveChangesAsync();
        }
        db.ActivityEvents.Add(new ActivityEvent {
            UserId = userId, Title = key, Message = device!.Id.ToString(),
            EventType = created ? PendingNew : PendingExisting
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(View(device));
    }

    static async Task<IResult> Complete(Guid attemptId, ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = UserId(principal);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var key = attemptId.ToString();
        var journal = await db.ActivityEvents.SingleOrDefaultAsync(e =>
            e.UserId == userId && e.Title == key && e.EventType.StartsWith("pairing."));
        if (journal is null || journal.EventType == Aborted)
            return Results.Conflict(new { message = "Eşleştirme işlemi bulunamadı veya iptal edildi." });
        if (journal.EventType == PendingNew) journal.EventType = CompleteNew;
        if (journal.EventType == PendingExisting) journal.EventType = CompleteExisting;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.NoContent();
    }

    static async Task<IResult> Abort(Guid attemptId, ClaimsPrincipal principal, AppDbContext db)
    {
        if (attemptId == Guid.Empty) return Results.BadRequest();
        var userId = UserId(principal);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var key = attemptId.ToString();
        var journal = await db.ActivityEvents.SingleOrDefaultAsync(e =>
            e.UserId == userId && e.Title == key && e.EventType.StartsWith("pairing."));
        if (journal is null)
        {
            // An abort can race the original PUT; do not allow late creation.
            journal = new ActivityEvent { UserId = userId, Title = key, Message = "" };
            db.ActivityEvents.Add(journal);
        }
        else if (OwnsNewRecord(journal.EventType))
        {
            var id = int.Parse(journal.Message);
            var device = await db.Devices.IgnoreQueryFilters().SingleOrDefaultAsync(d =>
                d.Id == id && d.Aquarium!.UserId == userId);
            if (device is not null) db.Devices.Remove(device);
            // No UsedDeviceRecord, ownership transfer or device.removed event.
        }
        journal.EventType = Aborted;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.NoContent();
    }
}
