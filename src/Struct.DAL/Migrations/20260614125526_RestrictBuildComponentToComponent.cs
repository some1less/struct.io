using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Struct.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RestrictBuildComponentToComponent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BuildComponents_Components_ComponentId",
                table: "BuildComponents");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildComponents_Components_ComponentId",
                table: "BuildComponents",
                column: "ComponentId",
                principalTable: "Components",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BuildComponents_Components_ComponentId",
                table: "BuildComponents");

            migrationBuilder.AddForeignKey(
                name: "FK_BuildComponents_Components_ComponentId",
                table: "BuildComponents",
                column: "ComponentId",
                principalTable: "Components",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
