using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORBIT.API.Migrations
{
    /// <inheritdoc />
    public partial class DeviceConnectionDescriptor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConnectionSsid",
                table: "Devices",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalApiBaseUrl",
                table: "Devices",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectionSsid",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LocalApiBaseUrl",
                table: "Devices");
        }
    }
}
