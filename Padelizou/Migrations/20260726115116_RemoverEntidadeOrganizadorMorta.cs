using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class RemoverEntidadeOrganizadorMorta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK__Torneio__Organiz__4E88ABD4",
                table: "Torneio");

            migrationBuilder.DropTable(
                name: "Organizador");

            migrationBuilder.DropIndex(
                name: "IX_Torneio_OrganizadorId",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "OrganizadorId",
                table: "Torneio");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrganizadorId",
                table: "Torneio",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Organizador",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    SenhaHash = table.Column<string>(type: "character varying(255)", unicode: false, maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Organiza__3214EC079F755F08", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Torneio_OrganizadorId",
                table: "Torneio",
                column: "OrganizadorId");

            migrationBuilder.CreateIndex(
                name: "UQ__Organiza__06370DAC46EF791B",
                table: "Organizador",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__Organiza__A9D10534817DC0DF",
                table: "Organizador",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK__Torneio__Organiz__4E88ABD4",
                table: "Torneio",
                column: "OrganizadorId",
                principalTable: "Organizador",
                principalColumn: "Id");
        }
    }
}
