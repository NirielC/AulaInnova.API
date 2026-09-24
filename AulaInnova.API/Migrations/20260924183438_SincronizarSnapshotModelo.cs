using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaInnova.API.Migrations
{
    /// <inheritdoc />
    public partial class SincronizarSnapshotModelo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_persona_PersonaId",
                schema: "dbo",
                table: "AspNetUsers");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                schema: "dbo",
                newName: "AspNetUsers");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_persona_PersonaId",
                table: "AspNetUsers",
                column: "PersonaId",
                principalSchema: "ai",
                principalTable: "persona",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_persona_PersonaId",
                table: "AspNetUsers");

            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                newName: "AspNetUsers",
                newSchema: "dbo");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_persona_PersonaId",
                schema: "dbo",
                table: "AspNetUsers",
                column: "PersonaId",
                principalSchema: "ai",
                principalTable: "persona",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
