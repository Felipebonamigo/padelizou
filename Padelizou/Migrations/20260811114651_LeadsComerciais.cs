using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class LeadsComerciais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeadsComerciais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParceiroId = table.Column<int>(type: "integer", nullable: false),
                    Contato = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Telefone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RegistradoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RegistradoPorId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClienteId = table.Column<int>(type: "integer", nullable: true),
                    MotivoPerda = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadsComerciais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadsComerciais_Jogador_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeadsComerciais_Jogador_ParceiroId",
                        column: x => x.ParceiroId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeadsComerciais_ClienteId",
                table: "LeadsComerciais",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadsComerciais_ParceiroId",
                table: "LeadsComerciais",
                column: "ParceiroId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadsComerciais_Telefone",
                table: "LeadsComerciais",
                column: "Telefone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeadsComerciais");
        }
    }
}
