using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ORBIT.API.Data;
using ORBIT.API.Models;

internal static class DeviceIdentityTests
{
    public static void Run()
    {
        static void Check(bool value, string message) {
            if (!value) throw new Exception(message);
        }
        var identity = new DeviceIdentity(DeviceIdentity.TwinsManufacturerId,
            Guid.Parse("00112233-4455-4677-8899-aabbccddeeff"), 1, 2, 1, new FirmwareVersion(1,0,0));
        Check(identity.IsValid, "Valid identity");
        var gridIdentity = identity with { ProductType = 1 };
        var gridConnection = new DeviceConnectionDescriptor(1, gridIdentity.ManufacturerId,
            gridIdentity.DeviceId, "ap", "GRID-legacy-name", "http://192.168.4.1");
        var gridRequest = new CreateDeviceRequest {
            Identity = gridIdentity, ConnectionDescriptor = gridConnection
        };
        Check(gridRequest.GetValidationError(out var gridType, out var gridName, out _) is null &&
            gridType == "GRID" && gridName == "GRID", "GRID uses generic identity/descriptor model");
        var gridDevice = new Device { Id = 42, AquariumId = 8 };
        gridIdentity.ApplyTo(gridDevice);
        gridConnection.ApplyTo(gridDevice);
        Check(DeviceIdentity.From(gridDevice) == gridIdentity &&
            DeviceConnectionDescriptor.From(gridDevice) == gridConnection,
            "GRID identity/descriptor storage round trip");
        Check(gridIdentity.Key == identity.Key, "Product does not create a second pairing key");
        Check(!System.Text.Json.JsonSerializer.Serialize(gridConnection).Contains(
            "password", StringComparison.OrdinalIgnoreCase), "GRID descriptor has no password");
        foreach (var blank in new string?[] { null, "", "   " })
        {
            var request = new CreateDeviceRequest { Identity=identity, Name=blank, SerialNumber=blank };
            Check(request.GetValidationError(out var type, out var name, out var serial) is null &&
                type=="DRIP" && name=="DRIP" && serial=="", "V1 needs neither name nor serial");
            Check(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request,
                new System.ComponentModel.DataAnnotations.ValidationContext(request), [], true),
                "DTO validation agrees with endpoint");
            Check(new CreateDeviceRequest { DeviceType="DRIP", Name=blank, SerialNumber="ABC" }
                .GetValidationError(out _, out _, out _) is not null, "Legacy name required");
            Check(new CreateDeviceRequest { DeviceType="DRIP", Name="Old", SerialNumber=blank }
                .GetValidationError(out _, out _, out _) is not null, "Legacy serial required");
        }
        var legacy = new CreateDeviceRequest { DeviceType="drip", Name=" Old ", SerialNumber=" abc " };
        Check(legacy.GetValidationError(out var legacyType, out var legacyName, out var legacySerial) is null &&
            legacyType=="DRIP" && legacyName=="Old" && legacySerial=="ABC", "Legacy normalization preserved");
        var named = new CreateDeviceRequest { Identity=identity, Name=" My pump " };
        Check(named.GetValidationError(out _, out var displayName, out _) is null &&
            displayName=="My pump", "V1 display name preserved");
        Check(new CreateDeviceRequest { Identity=identity with { DeviceId=Guid.Empty }, Name="Old", SerialNumber="ABC" }
            .GetValidationError(out _, out _, out _) is not null, "Invalid v1 cannot fall back to legacy");
        Check(new CreateDeviceRequest { Identity=identity, Name=new string('x',101) }
            .GetValidationError(out _, out _, out _) is not null, "Name storage limit preserved");

        var device = new Device { Id=12, AquariumId=8, Name="Kept name", SerialNumber="LEGACY" };
        identity.ApplyTo(device);
        Check(DeviceIdentity.From(device) == identity, "Identity round trip");
        var connection = new DeviceConnectionDescriptor(1, identity.ManufacturerId, identity.DeviceId,
            "ap", "renamed-ap", "http://192.168.4.1");
        Check(connection.Matches(identity), "Valid non-secret descriptor");
        Check(!(connection with {DeviceId=Guid.NewGuid()}).Matches(identity), "Descriptor identity binding");
        Check(!connection.Matches(null), "No descriptor for legacy identity");
        foreach (var url in new[]{"https://example.com", "http://127.0.0.1", "http://192.168.4.1/api",
            "http://user:password@192.168.4.1", "http://192.168.4.1/?key=secret"})
            Check(!(connection with {LocalApiBaseUrl=url}).Matches(identity), "Reject unsafe routing data");
        Check(!(connection with {Ssid=new string('ü',17)}).Matches(identity), "SSID limit is UTF-8 bytes");
        connection.ApplyTo(device);
        Check(DeviceConnectionDescriptor.From(device)==connection, "Descriptor storage round trip");
        Check(DeviceIdentity.From(device)==identity && device.Name=="Kept name", "Descriptor does not modify identity/display name");
        Check(new CreateDeviceRequest{Identity=identity, ConnectionDescriptor=connection with {DeviceId=Guid.Empty}}
            .GetValidationError(out _,out _,out _) is not null, "Endpoint validates descriptor");
        var connectionMigration = new ORBIT.API.Migrations.DeviceConnectionDescriptor();
        Check(connectionMigration.UpOperations.Count==2 && connectionMigration.UpOperations
            .OfType<AddColumnOperation>().All(c=>c.IsNullable && c.Table=="Devices"), "Only two nullable routing columns, no backfill");
        var upgraded = identity with { FirmwareVersion = new FirmwareVersion(2,3,4) };
        Check(upgraded.Key == identity.Key && upgraded.CompatibleHardware(device), "Firmware is not a key");
        upgraded.ApplyTo(device);
        Check(device.Id == 12 && device.AquariumId == 8 && device.Name=="Kept name" &&
            device.SerialNumber=="LEGACY" && device.FirmwareMajor==2, "Preserve existing record");
        Check((identity with { ProductType=1 }).Key == identity.Key, "Product not a key");
        Check(!(identity with { ProductType=1 }).CompatibleHardware(device), "Flag product conflict");
        Check((identity with { HardwareRevision=2 }).Key==identity.Key, "Hardware not a key");
        Check(!(identity with { HardwareRevision=2 }).CompatibleHardware(device), "Flag hardware conflict");
        Check(!(identity with { ManufacturerId=Guid.Empty }).IsValid, "Reject foreign manufacturer");
        Check(!(identity with { DeviceId=Guid.Empty }).IsValid, "Reject empty UUID");
        Check(!(identity with { IdentitySchemaVersion=2 }).IsValid, "Reject unknown schema");
        Check(!(identity with { FirmwareVersion=new FirmwareVersion(-1,0,0) }).IsValid, "Reject negative firmware");
        var history=new UsedDeviceRecord {SerialNumber="LEGACY",LastName="Kept name",LastUserId=4};
        DeviceIdentity.From(device)!.ApplyTo(history);
        Check(DeviceIdentity.From(history)!.Key==identity.Key && history.LastName=="Kept name",
            "Used-device identity and history preserved");
        Check(DeviceIdentity.From(new Device {SerialNumber="A472"}) is null, "No legacy UUID inference");

        using var db=new DesignTimeDbContextFactory().CreateDbContext([]);
        foreach(var type in new[]{typeof(Device),typeof(UsedDeviceRecord)}) {
            var entity=db.Model.FindEntityType(type)!;
            var unique=entity.GetIndexes().Single(i=>i.Properties.Select(p=>p.Name)
                .SequenceEqual(new[]{"ManufacturerId","ImmutableDeviceId"}));
            Check(unique.IsUnique && unique.GetFilter()!.Contains("IS NOT NULL"),"Filtered permanent key");
            Check(entity.GetIndexes().Single(i=>i.Properties.Count==1 &&
                i.Properties[0].Name=="SerialNumber").GetFilter()=="[ManufacturerId] IS NULL", "Legacy-only serial index");
        }
        var migration=new ORBIT.API.Migrations.DeviceIdentityV1();
        Check(migration.UpOperations.OfType<AddColumnOperation>().Count()==16, "8 nullable fields per table");
        Check(migration.UpOperations.OfType<AddColumnOperation>().All(c=>c.IsNullable), "Old rows stay legacy");
        Check(!migration.UpOperations.Any(o=>o is DropTableOperation or DropColumnOperation or SqlOperation),
            "No deletion or automatic legacy backfill in Up");
        // Generate SQL only: never open a database connection.
        var sql=db.GetService<IMigrator>().GenerateScript(
            "20260807185950_AddRegistrationEmailVerification", "20260831202030_DeviceIdentityV1");
        Check(sql.Contains("CREATE UNIQUE INDEX") && sql.Contains("[ManufacturerId], [ImmutableDeviceId]"),
            "Migration SQL contains the permanent key");
        Console.WriteLine("Device Identity policy/model/migration checks passed (no database connection).");
    }
}
