using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORBIT.API.Migrations
{
    /// <inheritdoc />
    public partial class AddGridTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceTelemetry",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UptimeSeconds = table.Column<long>(type: "bigint", nullable: false),
                    InputVoltage = table.Column<float>(type: "real", nullable: false),
                    TemperatureC = table.Column<float>(type: "real", nullable: true),
                    WaterAlarm = table.Column<bool>(type: "bit", nullable: false),
                    RelayMask = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTelemetry", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTelemetry_DeviceId_ReceivedAt",
                table: "DeviceTelemetry",
                columns: new[] { "DeviceId", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceTelemetry");
        }
    }
}
