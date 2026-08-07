using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class ErrosDoSistema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErroDoSistema",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuandoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Caminho = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Metodo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Mensagem = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Detalhe = table.Column<string>(type: "text", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: true),
                    AvisoEnviado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErroDoSistema", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErroDoSistema_QuandoEm",
                table: "ErroDoSistema",
                column: "QuandoEm");

            migrationBuilder.CreateIndex(
                name: "IX_ErroDoSistema_Tipo_Caminho_AvisoEnviado_QuandoEm",
                table: "ErroDoSistema",
                columns: new[] { "Tipo", "Caminho", "AvisoEnviado", "QuandoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErroDoSistema");
        }
    }
}
