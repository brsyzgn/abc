using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORBIT.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveActivityReadState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "ActivityEvents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "ActivityEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
