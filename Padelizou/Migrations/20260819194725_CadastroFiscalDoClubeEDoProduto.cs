using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class CadastroFiscalDoClubeEDoProduto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cest",
                table: "ProdutoBar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cfop",
                table: "ProdutoBar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoBarras",
                table: "ProdutoBar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CsosnOuCst",
                table: "ProdutoBar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ncm",
                table: "ProdutoBar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrigemMercadoria",
                table: "ProdutoBar",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoFiscal",
                table: "ProdutoBar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnidadeComercial",
                table: "ProdutoBar",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CpfConsumidor",
                table: "Comanda",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bairro",
                table: "Clubes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cep",
                table: "Clubes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CertificadoConfiguradoEm",
                table: "Clubes",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cnpj",
                table: "Clubes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Complemento",
                table: "Clubes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InscricaoEstadual",
                table: "Clubes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InscricaoMunicipal",
                table: "Clubes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Logradouro",
                table: "Clubes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MunicipioIbge",
                table: "Clubes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroEndereco",
                table: "Clubes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazaoSocial",
                table: "Clubes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegimeTributario",
                table: "Clubes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Uf",
                table: "Clubes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cest",
                table: "ProdutoBar");

            migrationBuilder.DropColumn(
                name: "Cfop",
                table: "ProdutoBar");

            migrationBuilder.DropColumn(
                name: "CodigoBarras",
                table: "ProdutoBar");

            migrationBuilder.DropColumn(
                name: "CsosnOuCst",
                table: "ProdutoBar");

            migrationBuilder.DropColumn(
                name: "Ncm",
                table: "ProdutoBar");

            migrationBuilder.DropColumn(
                name: "OrigemMercadoria",
                table: "ProdutoBar");

            migrationBuilder.DropColumn(
                name: "TipoFiscal",
                table: "ProdutoBar");

            migrationBuilder.DropColumn(
                name: "UnidadeComercial",
                table: "ProdutoBar");

            migrationBuilder.DropColumn(
                name: "CpfConsumidor",
                table: "Comanda");

            migrationBuilder.DropColumn(
                name: "Bairro",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "Cep",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "CertificadoConfiguradoEm",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "Cnpj",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "Complemento",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "InscricaoEstadual",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "InscricaoMunicipal",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "Logradouro",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "MunicipioIbge",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "NumeroEndereco",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "RazaoSocial",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "RegimeTributario",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "Uf",
                table: "Clubes");
        }
    }
}
