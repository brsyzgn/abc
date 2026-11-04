using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORBIT.API.Migrations
{
    /// <inheritdoc />
    public partial class AddGridDeviceCredentialsAndPresence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeviceCredentialCreatedAt",
                table: "Devices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceCredentialHash",
                table: "Devices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "Devices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_LastSeenAt",
                table: "Devices",
                column: "LastSeenAt",
                filter: "[LastSeenAt] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_LastSeenAt",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DeviceCredentialCreatedAt",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DeviceCredentialHash",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "Devices");
        }
    }
}
