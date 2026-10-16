using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ayurveda.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDemoToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDemo",
                table: "AspNetUsers");
        }
    }
}
