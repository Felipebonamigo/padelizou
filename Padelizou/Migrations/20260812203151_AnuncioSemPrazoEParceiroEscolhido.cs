using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AnuncioSemPrazoEParceiroEscolhido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "ValeAte",
                table: "AnuncioDeDesafio",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "ParceiroConfirmouEm",
                table: "AnuncioDeDesafio",
                type: "timestamp without time zone",
                nullable: true);

            // ⚠️ ESTE UPDATE NÃO É ENFEITE. Até aqui só existia UM jeito de a dupla fechar: o
            // parceiro aceitar pelo link. Toda linha que já tem `Jogador2Id` é, portanto, uma
            // dupla CONFIRMADA — e deixá-la com a coluna nova nula faria a tela cobrar dessas
            // pessoas uma confirmação que elas já deram, e oferecer a elas "sair da dupla" de um
            // anúncio que elas mesmas aceitaram.
            //
            // Usa a data de criação do anúncio, e não `now()`: o aceite aconteceu naquela época,
            // não no dia do deploy. Mesma disciplina do aceite dos Termos — não carimbar um
            // consentimento com a data de hoje.
            migrationBuilder.Sql(@"
                UPDATE ""AnuncioDeDesafio""
                SET ""ParceiroConfirmouEm"" = ""CriadoEm""
                WHERE ""Jogador2Id"" IS NOT NULL AND ""ParceiroConfirmouEm"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParceiroConfirmouEm",
                table: "AnuncioDeDesafio");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ValeAte",
                table: "AnuncioDeDesafio",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);
        }
    }
}
