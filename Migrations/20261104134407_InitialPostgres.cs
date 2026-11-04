using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ORBIT.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceTelemetry",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UptimeSeconds = table.Column<long>(type: "bigint", nullable: false),
                    InputVoltage = table.Column<float>(type: "real", nullable: false),
                    TemperatureC = table.Column<float>(type: "real", nullable: true),
                    WaterAlarm = table.Column<bool>(type: "boolean", nullable: false),
                    RelayMask = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTelemetry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsedDeviceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ManufacturerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImmutableDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdentitySchemaVersion = table.Column<int>(type: "integer", nullable: true),
                    ProductTypeCode = table.Column<int>(type: "integer", nullable: true),
                    HardwareRevision = table.Column<int>(type: "integer", nullable: true),
                    FirmwareMajor = table.Column<int>(type: "integer", nullable: true),
                    FirmwareMinor = table.Column<int>(type: "integer", nullable: true),
                    FirmwarePatch = table.Column<int>(type: "integer", nullable: true),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastUserId = table.Column<int>(type: "integer", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsedDeviceRecords", x => x.Id);
                    table.CheckConstraint("CK_UsedDeviceRecords_IdentityV1", "((\"ManufacturerId\" IS NULL AND \"ImmutableDeviceId\" IS NULL AND \"IdentitySchemaVersion\" IS NULL AND \"ProductTypeCode\" IS NULL AND \"HardwareRevision\" IS NULL AND \"FirmwareMajor\" IS NULL AND \"FirmwareMinor\" IS NULL AND \"FirmwarePatch\" IS NULL) OR (\"ManufacturerId\" IS NOT NULL AND \"ImmutableDeviceId\" IS NOT NULL AND \"IdentitySchemaVersion\" IS NOT NULL AND \"ProductTypeCode\" IS NOT NULL AND \"HardwareRevision\" IS NOT NULL AND \"FirmwareMajor\" IS NOT NULL AND \"FirmwareMinor\" IS NOT NULL AND \"FirmwarePatch\" IS NOT NULL AND \"ManufacturerId\" = '40b635de-f7d1-48e6-ae89-0977a3495e3e' AND \"ImmutableDeviceId\" <> '00000000-0000-0000-0000-000000000000' AND \"IdentitySchemaVersion\" = 1 AND \"ProductTypeCode\" IN (1,2,3) AND \"HardwareRevision\" BETWEEN 1 AND 65535 AND \"FirmwareMajor\" BETWEEN 0 AND 65535 AND \"FirmwareMinor\" BETWEEN 0 AND 65535 AND \"FirmwarePatch\" BETWEEN 0 AND 65535))");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ActivityEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Aquariums",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PhotoObjectKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aquariums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aquariums_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetChallenges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ResetTokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CodeVerifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResetTokenExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UsedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetChallenges_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationVerificationChallenges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationVerificationChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrationVerificationChallenges_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RememberMe = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConnectionSsid = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LocalApiBaseUrl = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ManufacturerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImmutableDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdentitySchemaVersion = table.Column<int>(type: "integer", nullable: true),
                    ProductTypeCode = table.Column<int>(type: "integer", nullable: true),
                    HardwareRevision = table.Column<int>(type: "integer", nullable: true),
                    FirmwareMajor = table.Column<int>(type: "integer", nullable: true),
                    FirmwareMinor = table.Column<int>(type: "integer", nullable: true),
                    FirmwarePatch = table.Column<int>(type: "integer", nullable: true),
                    DeviceCredentialHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DeviceCredentialCreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AquariumId = table.Column<int>(type: "integer", nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.CheckConstraint("CK_Devices_IdentityV1", "((\"ManufacturerId\" IS NULL AND \"ImmutableDeviceId\" IS NULL AND \"IdentitySchemaVersion\" IS NULL AND \"ProductTypeCode\" IS NULL AND \"HardwareRevision\" IS NULL AND \"FirmwareMajor\" IS NULL AND \"FirmwareMinor\" IS NULL AND \"FirmwarePatch\" IS NULL) OR (\"ManufacturerId\" IS NOT NULL AND \"ImmutableDeviceId\" IS NOT NULL AND \"IdentitySchemaVersion\" IS NOT NULL AND \"ProductTypeCode\" IS NOT NULL AND \"HardwareRevision\" IS NOT NULL AND \"FirmwareMajor\" IS NOT NULL AND \"FirmwareMinor\" IS NOT NULL AND \"FirmwarePatch\" IS NOT NULL AND \"ManufacturerId\" = '40b635de-f7d1-48e6-ae89-0977a3495e3e' AND \"ImmutableDeviceId\" <> '00000000-0000-0000-0000-000000000000' AND \"IdentitySchemaVersion\" = 1 AND \"ProductTypeCode\" IN (1,2,3) AND \"HardwareRevision\" BETWEEN 1 AND 65535 AND \"FirmwareMajor\" BETWEEN 0 AND 65535 AND \"FirmwareMinor\" BETWEEN 0 AND 65535 AND \"FirmwarePatch\" BETWEEN 0 AND 65535))");
                    table.ForeignKey(
                        name: "FK_Devices_Aquariums_AquariumId",
                        column: x => x.AquariumId,
                        principalTable: "Aquariums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_UserId_CreatedAt",
                table: "ActivityEvents",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Aquariums_UserId",
                table: "Aquariums",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_AquariumId",
                table: "Devices",
                column: "AquariumId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_LastSeenAt",
                table: "Devices",
                column: "LastSeenAt",
                filter: "\"LastSeenAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ManufacturerId_ImmutableDeviceId",
                table: "Devices",
                columns: new[] { "ManufacturerId", "ImmutableDeviceId" },
                unique: true,
                filter: "\"ManufacturerId\" IS NOT NULL AND \"ImmutableDeviceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices",
                column: "SerialNumber",
                unique: true,
                filter: "\"ManufacturerId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTelemetry_DeviceId_ReceivedAt",
                table: "DeviceTelemetry",
                columns: new[] { "DeviceId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetChallenges_UserId_CreatedAt",
                table: "PasswordResetChallenges",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationVerificationChallenges_UserId_CreatedAt",
                table: "RegistrationVerificationChallenges",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UsedDeviceRecords_ManufacturerId_ImmutableDeviceId",
                table: "UsedDeviceRecords",
                columns: new[] { "ManufacturerId", "ImmutableDeviceId" },
                unique: true,
                filter: "\"ManufacturerId\" IS NOT NULL AND \"ImmutableDeviceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UsedDeviceRecords_SerialNumber",
                table: "UsedDeviceRecords",
                column: "SerialNumber",
                unique: true,
                filter: "\"ManufacturerId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId_DeviceId",
                table: "UserSessions",
                columns: new[] { "UserId", "DeviceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityEvents");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "DeviceTelemetry");

            migrationBuilder.DropTable(
                name: "PasswordResetChallenges");

            migrationBuilder.DropTable(
                name: "RegistrationVerificationChallenges");

            migrationBuilder.DropTable(
                name: "UsedDeviceRecords");

            migrationBuilder.DropTable(
                name: "UserSessions");

            migrationBuilder.DropTable(
                name: "Aquariums");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
