using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORBIT.API.Migrations
{
    /// <inheritdoc />
    public partial class DeviceIdentityV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UsedDeviceRecords_SerialNumber",
                table: "UsedDeviceRecords");

            migrationBuilder.DropIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices");

            migrationBuilder.AddColumn<int>(
                name: "FirmwareMajor",
                table: "UsedDeviceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirmwareMinor",
                table: "UsedDeviceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirmwarePatch",
                table: "UsedDeviceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HardwareRevision",
                table: "UsedDeviceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdentitySchemaVersion",
                table: "UsedDeviceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImmutableDeviceId",
                table: "UsedDeviceRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManufacturerId",
                table: "UsedDeviceRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductTypeCode",
                table: "UsedDeviceRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirmwareMajor",
                table: "Devices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirmwareMinor",
                table: "Devices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirmwarePatch",
                table: "Devices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HardwareRevision",
                table: "Devices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdentitySchemaVersion",
                table: "Devices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImmutableDeviceId",
                table: "Devices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManufacturerId",
                table: "Devices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductTypeCode",
                table: "Devices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsedDeviceRecords_ManufacturerId_ImmutableDeviceId",
                table: "UsedDeviceRecords",
                columns: new[] { "ManufacturerId", "ImmutableDeviceId" },
                unique: true,
                filter: "[ManufacturerId] IS NOT NULL AND [ImmutableDeviceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UsedDeviceRecords_SerialNumber",
                table: "UsedDeviceRecords",
                column: "SerialNumber",
                unique: true,
                filter: "[ManufacturerId] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UsedDeviceRecords_IdentityV1",
                table: "UsedDeviceRecords",
                sql: "(([ManufacturerId] IS NULL AND [ImmutableDeviceId] IS NULL AND [IdentitySchemaVersion] IS NULL AND [ProductTypeCode] IS NULL AND [HardwareRevision] IS NULL AND [FirmwareMajor] IS NULL AND [FirmwareMinor] IS NULL AND [FirmwarePatch] IS NULL) OR ([ManufacturerId] IS NOT NULL AND [ImmutableDeviceId] IS NOT NULL AND [IdentitySchemaVersion] IS NOT NULL AND [ProductTypeCode] IS NOT NULL AND [HardwareRevision] IS NOT NULL AND [FirmwareMajor] IS NOT NULL AND [FirmwareMinor] IS NOT NULL AND [FirmwarePatch] IS NOT NULL AND [ManufacturerId] = '40b635de-f7d1-48e6-ae89-0977a3495e3e' AND [ImmutableDeviceId] <> '00000000-0000-0000-0000-000000000000' AND [IdentitySchemaVersion] = 1 AND [ProductTypeCode] IN (1,2,3) AND [HardwareRevision] BETWEEN 1 AND 65535 AND [FirmwareMajor] BETWEEN 0 AND 65535 AND [FirmwareMinor] BETWEEN 0 AND 65535 AND [FirmwarePatch] BETWEEN 0 AND 65535))");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ManufacturerId_ImmutableDeviceId",
                table: "Devices",
                columns: new[] { "ManufacturerId", "ImmutableDeviceId" },
                unique: true,
                filter: "[ManufacturerId] IS NOT NULL AND [ImmutableDeviceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices",
                column: "SerialNumber",
                unique: true,
                filter: "[ManufacturerId] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Devices_IdentityV1",
                table: "Devices",
                sql: "(([ManufacturerId] IS NULL AND [ImmutableDeviceId] IS NULL AND [IdentitySchemaVersion] IS NULL AND [ProductTypeCode] IS NULL AND [HardwareRevision] IS NULL AND [FirmwareMajor] IS NULL AND [FirmwareMinor] IS NULL AND [FirmwarePatch] IS NULL) OR ([ManufacturerId] IS NOT NULL AND [ImmutableDeviceId] IS NOT NULL AND [IdentitySchemaVersion] IS NOT NULL AND [ProductTypeCode] IS NOT NULL AND [HardwareRevision] IS NOT NULL AND [FirmwareMajor] IS NOT NULL AND [FirmwareMinor] IS NOT NULL AND [FirmwarePatch] IS NOT NULL AND [ManufacturerId] = '40b635de-f7d1-48e6-ae89-0977a3495e3e' AND [ImmutableDeviceId] <> '00000000-0000-0000-0000-000000000000' AND [IdentitySchemaVersion] = 1 AND [ProductTypeCode] IN (1,2,3) AND [HardwareRevision] BETWEEN 1 AND 65535 AND [FirmwareMajor] BETWEEN 0 AND 65535 AND [FirmwareMinor] BETWEEN 0 AND 65535 AND [FirmwarePatch] BETWEEN 0 AND 65535))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UsedDeviceRecords_ManufacturerId_ImmutableDeviceId",
                table: "UsedDeviceRecords");

            migrationBuilder.DropIndex(
                name: "IX_UsedDeviceRecords_SerialNumber",
                table: "UsedDeviceRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UsedDeviceRecords_IdentityV1",
                table: "UsedDeviceRecords");

            migrationBuilder.DropIndex(
                name: "IX_Devices_ManufacturerId_ImmutableDeviceId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Devices_IdentityV1",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "FirmwareMajor",
                table: "UsedDeviceRecords");

            migrationBuilder.DropColumn(
                name: "FirmwareMinor",
                table: "UsedDeviceRecords");

            migrationBuilder.DropColumn(
                name: "FirmwarePatch",
                table: "UsedDeviceRecords");

            migrationBuilder.DropColumn(
                name: "HardwareRevision",
                table: "UsedDeviceRecords");

            migrationBuilder.DropColumn(
                name: "IdentitySchemaVersion",
                table: "UsedDeviceRecords");

            migrationBuilder.DropColumn(
                name: "ImmutableDeviceId",
                table: "UsedDeviceRecords");

            migrationBuilder.DropColumn(
                name: "ManufacturerId",
                table: "UsedDeviceRecords");

            migrationBuilder.DropColumn(
                name: "ProductTypeCode",
                table: "UsedDeviceRecords");

            migrationBuilder.DropColumn(
                name: "FirmwareMajor",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "FirmwareMinor",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "FirmwarePatch",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "HardwareRevision",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "IdentitySchemaVersion",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ImmutableDeviceId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ManufacturerId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ProductTypeCode",
                table: "Devices");

            migrationBuilder.CreateIndex(
                name: "IX_UsedDeviceRecords_SerialNumber",
                table: "UsedDeviceRecords",
                column: "SerialNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SerialNumber",
                table: "Devices",
                column: "SerialNumber",
                unique: true);
        }
    }
}
