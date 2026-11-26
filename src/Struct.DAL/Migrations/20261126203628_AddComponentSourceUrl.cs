using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Struct.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddComponentSourceUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "Components",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "Components");
        }
    }
}
