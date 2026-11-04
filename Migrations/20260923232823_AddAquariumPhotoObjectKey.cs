using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ORBIT.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAquariumPhotoObjectKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoObjectKey",
                table: "Aquariums",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoObjectKey",
                table: "Aquariums");
        }
    }
}
