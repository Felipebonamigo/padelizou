using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class ParceiroOpcionalEMultiplasCategorias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue TRUE de propósito: antes desta coluna não havia trava nenhuma,
            // qualquer jogador podia entrar em quantas categorias quisesse. Subir com false
            // mudaria silenciosamente a regra de todos os torneios que já existem.
            migrationBuilder.AddColumn<bool>(
                name: "PermiteMultiplasCategorias",
                table: "Torneio",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<int>(
                name: "Jogador2Id",
                table: "Dupla",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PermiteMultiplasCategorias",
                table: "Torneio");

            migrationBuilder.AlterColumn<int>(
                name: "Jogador2Id",
                table: "Dupla",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
