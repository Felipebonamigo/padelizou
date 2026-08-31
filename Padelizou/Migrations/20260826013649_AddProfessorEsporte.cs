using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddProfessorEsporte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProfessorEsporte",
                columns: table => new
                {
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    Esporte = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessorEsporte", x => new { x.ProfessorId, x.Esporte });
                    table.ForeignKey(
                        name: "FK_ProfessorEsporte_Jogador_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfessorEsporte");
        }
    }
}
