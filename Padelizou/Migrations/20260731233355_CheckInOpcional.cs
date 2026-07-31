using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class CheckInOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue TRUE de propósito: os torneios que já estão no ar tinham o
            // check-in disponível, e migração não pode tirar função de quem já contava com
            // ela. O EF gera `false` sozinho (é o default do tipo bool); aqui a escolha é
            // preservar o comportamento, não o zero do tipo.
            migrationBuilder.AddColumn<bool>(
                name: "UsaCheckIn",
                table: "Torneio",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsaCheckIn",
                table: "Torneio");
        }
    }
}
