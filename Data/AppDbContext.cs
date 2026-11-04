using Microsoft.EntityFrameworkCore;
using ORBIT.API.Models;

namespace ORBIT.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Aquarium> Aquariums { get; set; }
    public DbSet<Device> Devices { get; set; }
    public DbSet<DeviceTelemetry> DeviceTelemetry { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }
    public DbSet<PasswordResetChallenge> PasswordResetChallenges { get; set; }
    public DbSet<RegistrationVerificationChallenge> RegistrationVerificationChallenges { get; set; }
    public DbSet<UsedDeviceRecord> UsedDeviceRecords { get; set; }
    public DbSet<ActivityEvent> ActivityEvents { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // The API was built against SQL Server datetime2: values were stored without a time zone and were
        // read back with Kind=Unspecified, so JSON timestamps carry no "Z". Keep exactly that contract on
        // PostgreSQL ("timestamp without time zone") instead of Npgsql's default timestamptz/UTC behavior.
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UnspecifiedDateTimeConverter>()
            .HaveColumnType("timestamp without time zone");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Pending pairing records are invisible to all normal lists/counts.
        // Transaction endpoints explicitly bypass this application-only filter.
        modelBuilder.Entity<Device>().HasQueryFilter(device => !ActivityEvents.Any(e =>
            e.EventType == "pairing.pending.new" && e.Message == device.Id.ToString()));
        modelBuilder.Entity<Device>()
            .HasIndex(device => device.SerialNumber)
            .IsUnique().HasFilter("\"ManufacturerId\" IS NULL");
        modelBuilder.Entity<UsedDeviceRecord>()
            .HasIndex(device => device.SerialNumber)
            .IsUnique().HasFilter("\"ManufacturerId\" IS NULL");
        modelBuilder.Entity<Device>().HasIndex(d => new { d.ManufacturerId, d.ImmutableDeviceId })
            .IsUnique().HasFilter("\"ManufacturerId\" IS NOT NULL AND \"ImmutableDeviceId\" IS NOT NULL");
        modelBuilder.Entity<Device>()
            .HasIndex(device => device.LastSeenAt)
            .HasFilter("\"LastSeenAt\" IS NOT NULL");
        modelBuilder.Entity<DeviceTelemetry>()
            .HasIndex(sample => new { sample.DeviceId, sample.ReceivedAt });
        modelBuilder.Entity<Device>().ToTable("Devices", t => t.HasCheckConstraint("CK_Devices_IdentityV1", "((\"ManufacturerId\" IS NULL AND \"ImmutableDeviceId\" IS NULL AND \"IdentitySchemaVersion\" IS NULL AND \"ProductTypeCode\" IS NULL AND \"HardwareRevision\" IS NULL AND \"FirmwareMajor\" IS NULL AND \"FirmwareMinor\" IS NULL AND \"FirmwarePatch\" IS NULL) OR (\"ManufacturerId\" IS NOT NULL AND \"ImmutableDeviceId\" IS NOT NULL AND \"IdentitySchemaVersion\" IS NOT NULL AND \"ProductTypeCode\" IS NOT NULL AND \"HardwareRevision\" IS NOT NULL AND \"FirmwareMajor\" IS NOT NULL AND \"FirmwareMinor\" IS NOT NULL AND \"FirmwarePatch\" IS NOT NULL AND \"ManufacturerId\" = '40b635de-f7d1-48e6-ae89-0977a3495e3e' AND \"ImmutableDeviceId\" <> '00000000-0000-0000-0000-000000000000' AND \"IdentitySchemaVersion\" = 1 AND \"ProductTypeCode\" IN (1,2,3) AND \"HardwareRevision\" BETWEEN 1 AND 65535 AND \"FirmwareMajor\" BETWEEN 0 AND 65535 AND \"FirmwareMinor\" BETWEEN 0 AND 65535 AND \"FirmwarePatch\" BETWEEN 0 AND 65535))"));
        modelBuilder.Entity<UsedDeviceRecord>().HasIndex(d => new { d.ManufacturerId, d.ImmutableDeviceId })
            .IsUnique().HasFilter("\"ManufacturerId\" IS NOT NULL AND \"ImmutableDeviceId\" IS NOT NULL");
        modelBuilder.Entity<UsedDeviceRecord>().ToTable("UsedDeviceRecords", t => t.HasCheckConstraint("CK_UsedDeviceRecords_IdentityV1", "((\"ManufacturerId\" IS NULL AND \"ImmutableDeviceId\" IS NULL AND \"IdentitySchemaVersion\" IS NULL AND \"ProductTypeCode\" IS NULL AND \"HardwareRevision\" IS NULL AND \"FirmwareMajor\" IS NULL AND \"FirmwareMinor\" IS NULL AND \"FirmwarePatch\" IS NULL) OR (\"ManufacturerId\" IS NOT NULL AND \"ImmutableDeviceId\" IS NOT NULL AND \"IdentitySchemaVersion\" IS NOT NULL AND \"ProductTypeCode\" IS NOT NULL AND \"HardwareRevision\" IS NOT NULL AND \"FirmwareMajor\" IS NOT NULL AND \"FirmwareMinor\" IS NOT NULL AND \"FirmwarePatch\" IS NOT NULL AND \"ManufacturerId\" = '40b635de-f7d1-48e6-ae89-0977a3495e3e' AND \"ImmutableDeviceId\" <> '00000000-0000-0000-0000-000000000000' AND \"IdentitySchemaVersion\" = 1 AND \"ProductTypeCode\" IN (1,2,3) AND \"HardwareRevision\" BETWEEN 1 AND 65535 AND \"FirmwareMajor\" BETWEEN 0 AND 65535 AND \"FirmwareMinor\" BETWEEN 0 AND 65535 AND \"FirmwarePatch\" BETWEEN 0 AND 65535))"));
        modelBuilder.Entity<UserSession>()
            .HasIndex(session => new { session.UserId, session.DeviceId });
        modelBuilder.Entity<PasswordResetChallenge>()
            .HasIndex(challenge => new { challenge.UserId, challenge.CreatedAt });
        modelBuilder.Entity<RegistrationVerificationChallenge>()
            .HasIndex(challenge => new { challenge.UserId, challenge.CreatedAt });
        modelBuilder.Entity<ActivityEvent>()
            .HasIndex(activity => new { activity.UserId, activity.CreatedAt });
    }
}
