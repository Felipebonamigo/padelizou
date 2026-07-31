using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class EstoqueDoBar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ControlaEstoque",
                table: "ProdutoBar",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EstoqueMinimo",
                table: "ProdutoBar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UnidadesPorEmbalagem",
                table: "ProdutoBar",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MovimentoEstoque",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProdutoBarId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    CustoUnitario = table.Column<decimal>(type: "numeric", nullable: true),
                    ItemComandaId = table.Column<int>(type: "integer", nullable: true),
                    Motivo = table.Column<string>(type: "text", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CriadoPorId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentoEstoque", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentoEstoque_ProdutoBar_ProdutoBarId",
                        column: x => x.ProdutoBarId,
                        principalTable: "ProdutoBar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentoEstoque_ItemComandaId",
                table: "MovimentoEstoque",
                column: "ItemComandaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentoEstoque_ProdutoBarId_CriadoEm",
                table: "MovimentoEstoque",
                columns: new[] { "ProdutoBarId", "CriadoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimentoEstoque");

            migrationBuilder.DropColumn(
                name: "ControlaEstoque",
                table: "ProdutoBar");

            migrationBuilder.DropColumn(
                name: "EstoqueMinimo",
                table: "ProdutoBar");

            migrationBuilder.DropColumn(
                name: "UnidadesPorEmbalagem",
                table: "ProdutoBar");
        }
    }
}
