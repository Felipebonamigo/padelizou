using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddAulasMarcacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Local",
                table: "Aula");

            migrationBuilder.AddColumn<string>(
                name: "GoogleEventId",
                table: "Aula",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LocalAulaId",
                table: "Aula",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TokenConfirmacao",
                table: "Aula",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "LocalAula",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfessorId = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Endereco = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrecoPadrao = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalAula", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocalAula_Jogador_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HorarioDisponivel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfessorId = table.Column<int>(type: "int", nullable: false),
                    LocalAulaId = table.Column<int>(type: "int", nullable: false),
                    DiaSemana = table.Column<int>(type: "int", nullable: false),
                    HoraInicio = table.Column<TimeSpan>(type: "time", nullable: false),
                    HoraFim = table.Column<TimeSpan>(type: "time", nullable: false),
                    DuracaoMinutos = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HorarioDisponivel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HorarioDisponivel_Jogador_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HorarioDisponivel_LocalAula_LocalAulaId",
                        column: x => x.LocalAulaId,
                        principalTable: "LocalAula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aula_LocalAulaId",
                table: "Aula",
                column: "LocalAulaId");

            migrationBuilder.CreateIndex(
                name: "IX_Aula_TokenConfirmacao",
                table: "Aula",
                column: "TokenConfirmacao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HorarioDisponivel_LocalAulaId",
                table: "HorarioDisponivel",
                column: "LocalAulaId");

            migrationBuilder.CreateIndex(
                name: "IX_HorarioDisponivel_ProfessorId",
                table: "HorarioDisponivel",
                column: "ProfessorId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalAula_ProfessorId",
                table: "LocalAula",
                column: "ProfessorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Aula_LocalAula_LocalAulaId",
                table: "Aula",
                column: "LocalAulaId",
                principalTable: "LocalAula",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Aula_LocalAula_LocalAulaId",
                table: "Aula");

            migrationBuilder.DropTable(
                name: "HorarioDisponivel");

            migrationBuilder.DropTable(
                name: "LocalAula");

            migrationBuilder.DropIndex(
                name: "IX_Aula_LocalAulaId",
                table: "Aula");

            migrationBuilder.DropIndex(
                name: "IX_Aula_TokenConfirmacao",
                table: "Aula");

            migrationBuilder.DropColumn(
                name: "GoogleEventId",
                table: "Aula");

            migrationBuilder.DropColumn(
                name: "LocalAulaId",
                table: "Aula");

            migrationBuilder.DropColumn(
                name: "TokenConfirmacao",
                table: "Aula");

            migrationBuilder.AddColumn<string>(
                name: "Local",
                table: "Aula",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
